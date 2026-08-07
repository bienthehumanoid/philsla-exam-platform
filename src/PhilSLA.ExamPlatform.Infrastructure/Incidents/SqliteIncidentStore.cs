using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Infrastructure.Incidents;

public sealed class SqliteIncidentStore : IIncidentStore
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegSignature = [255, 216, 255];

    private readonly string _databasePath;
    private readonly string _evidenceRoot;
    private readonly Func<SqliteConnection, CancellationToken, ValueTask<SqliteTransaction>> _beginTransactionAsync;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _creationLock = new(1, 1);
    private bool _initialized;

    public SqliteIncidentStore(string databasePath, string evidenceRoot)
        : this(databasePath, evidenceRoot, BeginTransactionAsync)
    {
    }

    internal SqliteIncidentStore(
        string databasePath,
        string evidenceRoot,
        Func<SqliteConnection, CancellationToken, ValueTask<SqliteTransaction>> beginTransactionAsync)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("A database path is required.", nameof(databasePath))
            : databasePath;
        _evidenceRoot = string.IsNullOrWhiteSpace(evidenceRoot)
            ? throw new ArgumentException("An evidence path is required.", nameof(evidenceRoot))
            : evidenceRoot;
        _beginTransactionAsync = beginTransactionAsync ?? throw new ArgumentNullException(nameof(beginTransactionAsync));
    }

    public async Task<IReadOnlyList<IncidentRecord>> LoadForSessionsAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (sessionIds.Count == 0)
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var records = await LoadRecordsAsync(connection, transaction, sessionIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return records;
    }

    public async Task<IncidentRecord> CreateAsync(
        IncidentRecord draft,
        IReadOnlyList<IncidentEvidenceUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(uploads);
        await EnsureInitializedAsync(cancellationToken);
        await _creationLock.WaitAsync(cancellationToken);
        string? finalDirectory = null;
        var committed = false;
        try
        {
            var prepared = await PrepareEvidenceAsync(draft.Id, uploads, cancellationToken);
            finalDirectory = prepared.FinalDirectory;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await _beginTransactionAsync(connection, cancellationToken);
            try
            {
                var sequence = await AllocateSequenceAsync(
                    connection,
                    transaction,
                    draft.CreatedAtUtc.Year,
                    cancellationToken);
                var displayId = $"INC-{draft.CreatedAtUtc.Year}-{sequence:000}";
                await InsertIncidentAsync(
                    connection,
                    transaction,
                    draft,
                    displayId,
                    cancellationToken);
                foreach (var attachment in prepared.Attachments)
                {
                    await InsertAttachmentAsync(
                        connection,
                        transaction,
                        draft.Id,
                        attachment,
                        cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return draft with
                {
                    DisplayId = displayId,
                    Attachments = prepared.Attachments
                };
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch
        {
            if (!committed)
            {
                DeleteDirectory(finalDirectory);
            }

            throw;
        }
        finally
        {
            _creationLock.Release();
        }
    }

    public async Task<byte[]> ReadEvidenceAsync(
        Guid incidentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT relative_path
            FROM incident_attachments
            WHERE incident_id = $incidentId AND attachment_id = $attachmentId;
            """;
        command.Parameters.AddWithValue("$incidentId", incidentId.ToString());
        command.Parameters.AddWithValue("$attachmentId", attachmentId.ToString());
        var relativePath = (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new FileNotFoundException("The incident evidence could not be found.");
        var incidentDirectory = Path.GetFullPath(Path.Combine(_evidenceRoot, incidentId.ToString("N")));
        var fullPath = Path.GetFullPath(Path.Combine(incidentDirectory, relativePath));
        if (!fullPath.StartsWith(incidentDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The incident evidence path is invalid.");
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var databaseDirectory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            Directory.CreateDirectory(_evidenceRoot);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;

                CREATE TABLE IF NOT EXISTS incident_sequences (
                    sequence_year INTEGER PRIMARY KEY,
                    last_value INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS incident_records (
                    incident_id TEXT PRIMARY KEY,
                    display_id TEXT NOT NULL UNIQUE,
                    session_id TEXT NOT NULL,
                    session_title TEXT NOT NULL,
                    room TEXT NOT NULL,
                    candidate_id TEXT NOT NULL,
                    student_number TEXT NOT NULL,
                    candidate_name TEXT NOT NULL,
                    category_id TEXT NOT NULL,
                    category_name TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    description TEXT NOT NULL,
                    review_status TEXT NOT NULL,
                    reported_by_proctor_id TEXT NOT NULL,
                    reported_by_proctor_name TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_incident_records_session_created
                    ON incident_records(session_id, created_at_utc DESC);

                CREATE TABLE IF NOT EXISTS incident_attachments (
                    attachment_id TEXT PRIMARY KEY,
                    incident_id TEXT NOT NULL,
                    original_file_name TEXT NOT NULL,
                    media_type TEXT NOT NULL,
                    byte_length INTEGER NOT NULL,
                    relative_path TEXT NOT NULL,
                    sha256 TEXT NOT NULL,
                    FOREIGN KEY (incident_id) REFERENCES incident_records(incident_id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_incident_attachments_incident
                    ON incident_attachments(incident_id);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await CleanupOrphansAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task CleanupOrphansAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        DeleteDirectory(Path.Combine(_evidenceRoot, ".staging"));
        var persistedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT incident_id FROM incident_records;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                persistedIds.Add(Guid.Parse(reader.GetString(0)).ToString("N"));
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(_evidenceRoot))
        {
            var name = Path.GetFileName(directory);
            if (!persistedIds.Contains(name))
            {
                DeleteDirectory(directory);
            }
        }
    }

    private async Task<PreparedEvidence> PrepareEvidenceAsync(
        Guid incidentId,
        IReadOnlyList<IncidentEvidenceUpload> uploads,
        CancellationToken cancellationToken)
    {
        if (uploads.Count == 0)
        {
            return new PreparedEvidence([], null);
        }

        var stagingDirectory = Path.Combine(_evidenceRoot, ".staging", incidentId.ToString("N"));
        var finalDirectory = Path.Combine(_evidenceRoot, incidentId.ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var attachments = new List<IncidentAttachment>(uploads.Count);
            foreach (var upload in uploads)
            {
                var attachmentId = Guid.NewGuid();
                var extension = upload.MediaType == "image/png" ? ".png" : ".jpg";
                var storedName = $"{attachmentId:N}{extension}";
                var stagedPath = Path.Combine(stagingDirectory, storedName);
                var hash = await CopyVerifiedAsync(upload, stagedPath, cancellationToken);
                attachments.Add(new IncidentAttachment(
                    attachmentId,
                    upload.FileName,
                    upload.MediaType,
                    upload.Length,
                    storedName,
                    hash));
            }

            Directory.Move(stagingDirectory, finalDirectory);
            return new PreparedEvidence(attachments.ToArray(), finalDirectory);
        }
        catch
        {
            DeleteDirectory(stagingDirectory);
            DeleteDirectory(finalDirectory);
            throw;
        }
    }

    private static async Task<string> CopyVerifiedAsync(
        IncidentEvidenceUpload upload,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = await upload.OpenReadAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var signatureLength = upload.MediaType == "image/png" ? PngSignature.Length : JpegSignature.Length;
        var signature = new byte[signatureLength];
        var signatureRead = 0;
        while (signatureRead < signature.Length)
        {
            var read = await source.ReadAsync(signature.AsMemory(signatureRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            signatureRead += read;
        }

        var expected = upload.MediaType == "image/png" ? PngSignature : JpegSignature;
        if (signatureRead != signature.Length || !signature.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidDataException("The evidence file content does not match its image type.");
        }

        await destination.WriteAsync(signature, cancellationToken);
        hash.AppendData(signature);
        var total = signature.Length;
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += count;
            if (total > upload.Length)
            {
                throw new InvalidDataException("The evidence file length does not match its metadata.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            hash.AppendData(buffer, 0, count);
        }

        if (total != upload.Length)
        {
            throw new InvalidDataException("The evidence file length does not match its metadata.");
        }

        await destination.FlushAsync(cancellationToken);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task<int> AllocateSequenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO incident_sequences (sequence_year, last_value)
            VALUES ($year, 1)
            ON CONFLICT(sequence_year)
            DO UPDATE SET last_value = last_value + 1
            RETURNING last_value;
            """;
        command.Parameters.AddWithValue("$year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task InsertIncidentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IncidentRecord record,
        string displayId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO incident_records (
                incident_id, display_id, session_id, session_title, room,
                candidate_id, student_number, candidate_name, category_id, category_name,
                severity, description, review_status, reported_by_proctor_id,
                reported_by_proctor_name, created_at_utc)
            VALUES (
                $incidentId, $displayId, $sessionId, $sessionTitle, $room,
                $candidateId, $studentNumber, $candidateName, $categoryId, $categoryName,
                $severity, $description, $reviewStatus, $proctorId, $proctorName, $createdAtUtc);
            """;
        command.Parameters.AddWithValue("$incidentId", record.Id.ToString());
        command.Parameters.AddWithValue("$displayId", displayId);
        command.Parameters.AddWithValue("$sessionId", record.SessionId.ToString());
        command.Parameters.AddWithValue("$sessionTitle", record.SessionTitle);
        command.Parameters.AddWithValue("$room", record.Room);
        command.Parameters.AddWithValue("$candidateId", record.CandidateId.ToString());
        command.Parameters.AddWithValue("$studentNumber", record.StudentNumber);
        command.Parameters.AddWithValue("$candidateName", record.CandidateName);
        command.Parameters.AddWithValue("$categoryId", record.CategoryId.ToString());
        command.Parameters.AddWithValue("$categoryName", record.CategoryName);
        command.Parameters.AddWithValue("$severity", record.Severity.ToString());
        command.Parameters.AddWithValue("$description", record.Description);
        command.Parameters.AddWithValue("$reviewStatus", record.ReviewStatus.ToString());
        command.Parameters.AddWithValue("$proctorId", record.ReportedByProctorId.ToString());
        command.Parameters.AddWithValue("$proctorName", record.ReportedByProctorName);
        command.Parameters.AddWithValue("$createdAtUtc", Format(record.CreatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAttachmentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid incidentId,
        IncidentAttachment attachment,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO incident_attachments (
                attachment_id, incident_id, original_file_name, media_type,
                byte_length, relative_path, sha256)
            VALUES (
                $attachmentId, $incidentId, $originalFileName, $mediaType,
                $byteLength, $relativePath, $sha256);
            """;
        command.Parameters.AddWithValue("$attachmentId", attachment.Id.ToString());
        command.Parameters.AddWithValue("$incidentId", incidentId.ToString());
        command.Parameters.AddWithValue("$originalFileName", attachment.OriginalFileName);
        command.Parameters.AddWithValue("$mediaType", attachment.MediaType);
        command.Parameters.AddWithValue("$byteLength", attachment.Length);
        command.Parameters.AddWithValue("$relativePath", attachment.RelativePath);
        command.Parameters.AddWithValue("$sha256", attachment.Sha256);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<IncidentRecord>> LoadRecordsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var parameters = sessionIds.Select((_, index) => $"$session{index}").ToArray();
        command.CommandText =
            $"""
            SELECT incident_id, display_id, session_id, session_title, room,
                   candidate_id, student_number, candidate_name, category_id, category_name,
                   severity, description, review_status, reported_by_proctor_id,
                   reported_by_proctor_name, created_at_utc
            FROM incident_records
            WHERE session_id IN ({string.Join(", ", parameters)})
            ORDER BY created_at_utc DESC, display_id DESC;
            """;
        var sessionIdArray = sessionIds.ToArray();
        for (var index = 0; index < sessionIdArray.Length; index++)
        {
            command.Parameters.AddWithValue(parameters[index], sessionIdArray[index].ToString());
        }

        var rows = new List<IncidentRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new IncidentRow(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    Guid.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    Guid.Parse(reader.GetString(5)),
                    reader.GetString(6),
                    reader.GetString(7),
                    Guid.Parse(reader.GetString(8)),
                    reader.GetString(9),
                    Enum.Parse<IncidentSeverity>(reader.GetString(10)),
                    reader.GetString(11),
                    Enum.Parse<IncidentReviewStatus>(reader.GetString(12)),
                    Guid.Parse(reader.GetString(13)),
                    reader.GetString(14),
                    Parse(reader.GetString(15))));
            }
        }

        var attachments = await LoadAttachmentsAsync(
            connection,
            transaction,
            rows.Select(row => row.Id).ToArray(),
            cancellationToken);
        return rows.Select(row => row.ToRecord(
            attachments.TryGetValue(row.Id, out var values) ? values : [])).ToArray();
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<IncidentAttachment>>> LoadAttachmentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<Guid> incidentIds,
        CancellationToken cancellationToken)
    {
        if (incidentIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var parameters = incidentIds.Select((_, index) => $"$incident{index}").ToArray();
        command.CommandText =
            $"""
            SELECT incident_id, attachment_id, original_file_name, media_type,
                   byte_length, relative_path, sha256
            FROM incident_attachments
            WHERE incident_id IN ({string.Join(", ", parameters)})
            ORDER BY rowid;
            """;
        for (var index = 0; index < incidentIds.Count; index++)
        {
            command.Parameters.AddWithValue(parameters[index], incidentIds[index].ToString());
        }

        var result = new Dictionary<Guid, IReadOnlyList<IncidentAttachment>>();
        var mutable = new Dictionary<Guid, List<IncidentAttachment>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var incidentId = Guid.Parse(reader.GetString(0));
            if (!mutable.TryGetValue(incidentId, out var list))
            {
                list = [];
                mutable.Add(incidentId, list);
            }

            list.Add(new IncidentAttachment(
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        foreach (var (key, value) in mutable)
        {
            result.Add(key, value.ToArray());
        }

        return result;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = 5
        };
        return new SqliteConnection(builder.ToString());
    }

    private static async ValueTask<SqliteTransaction> BeginTransactionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void DeleteDirectory(string? path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record PreparedEvidence(
        IReadOnlyList<IncidentAttachment> Attachments,
        string? FinalDirectory);

    private sealed record IncidentRow(
        Guid Id,
        string DisplayId,
        Guid SessionId,
        string SessionTitle,
        string Room,
        Guid CandidateId,
        string StudentNumber,
        string CandidateName,
        Guid CategoryId,
        string CategoryName,
        IncidentSeverity Severity,
        string Description,
        IncidentReviewStatus ReviewStatus,
        Guid ReportedByProctorId,
        string ReportedByProctorName,
        DateTimeOffset CreatedAtUtc)
    {
        public IncidentRecord ToRecord(IReadOnlyList<IncidentAttachment> attachments) => new(
            Id,
            DisplayId,
            SessionId,
            SessionTitle,
            Room,
            CandidateId,
            StudentNumber,
            CandidateName,
            CategoryId,
            CategoryName,
            Severity,
            Description,
            ReviewStatus,
            ReportedByProctorId,
            ReportedByProctorName,
            CreatedAtUtc,
            attachments);
    }
}
