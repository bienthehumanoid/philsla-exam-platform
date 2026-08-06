using System.Globalization;
using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Infrastructure.Attendance;

public sealed class SqliteAttendanceStore(string databasePath) : IAttendanceStore
{
    private const string ConflictMessage =
        "The attendance record was changed by another operation.";

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Func<CancellationToken, Task>? _afterSessionHeaderRead;
    private bool _initialized;

    internal SqliteAttendanceStore(
        string databasePath,
        Func<CancellationToken, Task> afterSessionHeaderRead)
        : this(databasePath)
    {
        _afterSessionHeaderRead = afterSessionHeaderRead
            ?? throw new ArgumentNullException(nameof(afterSessionHeaderRead));
    }

    public async Task<AttendanceSessionRecord?> LoadAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var record = await LoadAsync(
            connection,
            transaction,
            sessionId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return record;
    }

    public async Task<AttendanceSessionRecord> CreateAsync(
        AttendanceSessionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        int created;
        await using (var sessionCommand = connection.CreateCommand())
        {
            sessionCommand.Transaction = transaction;
            sessionCommand.CommandText =
                """
                INSERT INTO attendance_sessions (
                    session_id,
                    version,
                    finalized_at_utc
                )
                VALUES ($sessionId, 0, NULL)
                ON CONFLICT(session_id) DO NOTHING;
                """;
            sessionCommand.Parameters.AddWithValue(
                "$sessionId",
                definition.Id.ToString());
            created = await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (created == 1)
        {
            foreach (var student in definition.Students)
            {
                await InsertInitialEntryAsync(
                    connection,
                    transaction,
                    definition.Id,
                    student.Id,
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            definition.Id,
            cancellationToken);
    }

    public async Task<AttendanceSessionRecord> SaveAsync(
        AttendanceSessionRecord record,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var sessionCommand = connection.CreateCommand())
        {
            sessionCommand.Transaction = transaction;
            sessionCommand.CommandText =
                """
                UPDATE attendance_sessions
                SET version = $version,
                    finalized_at_utc = $finalizedAtUtc
                WHERE session_id = $sessionId
                  AND version = $expectedVersion;
                """;
            sessionCommand.Parameters.AddWithValue("$version", record.Version);
            sessionCommand.Parameters.AddWithValue(
                "$finalizedAtUtc",
                record.FinalizedAtUtc.HasValue
                    ? Format(record.FinalizedAtUtc.Value)
                    : DBNull.Value);
            sessionCommand.Parameters.AddWithValue(
                "$sessionId",
                record.SessionId.ToString());
            sessionCommand.Parameters.AddWithValue(
                "$expectedVersion",
                expectedVersion);
            if (await sessionCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(ConflictMessage);
            }
        }

        foreach (var entry in record.Entries)
        {
            await UpsertEntryAsync(
                connection,
                transaction,
                record.SessionId,
                entry,
                cancellationToken);
        }

        foreach (var auditEntry in record.AuditEntries)
        {
            await InsertAuditEntryAsync(
                connection,
                transaction,
                record.SessionId,
                auditEntry,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            record.SessionId,
            cancellationToken);
    }

    private async Task EnsureInitializedAsync(
        CancellationToken cancellationToken)
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

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;

                CREATE TABLE IF NOT EXISTS attendance_sessions (
                    session_id TEXT PRIMARY KEY,
                    version INTEGER NOT NULL,
                    finalized_at_utc TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS attendance_entries (
                    session_id TEXT NOT NULL,
                    student_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    check_in_method TEXT NULL,
                    received_at_utc TEXT NULL,
                    credential_id TEXT NULL,
                    manual_reason TEXT NULL,
                    confirmed_by_proctor_id TEXT NULL,
                    PRIMARY KEY (session_id, student_id),
                    FOREIGN KEY (session_id) REFERENCES attendance_sessions(session_id)
                );

                CREATE TABLE IF NOT EXISTS attendance_audit (
                    event_id TEXT PRIMARY KEY,
                    session_id TEXT NOT NULL,
                    student_id TEXT NOT NULL,
                    prior_status TEXT NOT NULL,
                    new_status TEXT NOT NULL,
                    reason TEXT NOT NULL,
                    proctor_id TEXT NOT NULL,
                    occurred_at_utc TEXT NOT NULL,
                    FOREIGN KEY (session_id) REFERENCES attendance_sessions(session_id)
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static async Task InsertInitialEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO attendance_entries (
                session_id,
                student_id,
                status
            )
            VALUES ($sessionId, $studentId, $status);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());
        command.Parameters.AddWithValue("$studentId", studentId.ToString());
        command.Parameters.AddWithValue(
            "$status",
            AttendanceStatus.Unmarked.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        AttendanceEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO attendance_entries (
                session_id,
                student_id,
                status,
                check_in_method,
                received_at_utc,
                credential_id,
                manual_reason,
                confirmed_by_proctor_id
            )
            VALUES (
                $sessionId,
                $studentId,
                $status,
                $checkInMethod,
                $receivedAtUtc,
                $credentialId,
                $manualReason,
                $confirmedByProctorId
            )
            ON CONFLICT(session_id, student_id)
            DO UPDATE SET
                status = excluded.status,
                check_in_method = excluded.check_in_method,
                received_at_utc = excluded.received_at_utc,
                credential_id = excluded.credential_id,
                manual_reason = excluded.manual_reason,
                confirmed_by_proctor_id = excluded.confirmed_by_proctor_id;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());
        command.Parameters.AddWithValue("$studentId", entry.StudentId.ToString());
        command.Parameters.AddWithValue("$status", entry.Status.ToString());
        command.Parameters.AddWithValue(
            "$checkInMethod",
            entry.CheckInMethod.HasValue
                ? entry.CheckInMethod.Value.ToString()
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$receivedAtUtc",
            entry.ReceivedAtUtc.HasValue
                ? Format(entry.ReceivedAtUtc.Value)
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "$credentialId",
            (object?)entry.CredentialId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$manualReason",
            (object?)entry.ManualReason ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$confirmedByProctorId",
            entry.ConfirmedByProctorId.HasValue
                ? entry.ConfirmedByProctorId.Value.ToString()
                : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        AttendanceAuditEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO attendance_audit (
                event_id,
                session_id,
                student_id,
                prior_status,
                new_status,
                reason,
                proctor_id,
                occurred_at_utc
            )
            VALUES (
                $eventId,
                $sessionId,
                $studentId,
                $priorStatus,
                $newStatus,
                $reason,
                $proctorId,
                $occurredAtUtc
            )
            ON CONFLICT(event_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$eventId", entry.EventId.ToString());
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());
        command.Parameters.AddWithValue("$studentId", entry.StudentId.ToString());
        command.Parameters.AddWithValue("$priorStatus", entry.PriorStatus.ToString());
        command.Parameters.AddWithValue("$newStatus", entry.NewStatus.ToString());
        command.Parameters.AddWithValue("$reason", entry.Reason);
        command.Parameters.AddWithValue("$proctorId", entry.ProctorId.ToString());
        command.Parameters.AddWithValue("$occurredAtUtc", Format(entry.OccurredAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AttendanceSessionRecord?> LoadAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        int version;
        DateTimeOffset? finalizedAtUtc;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT version, finalized_at_utc
                FROM attendance_sessions
                WHERE session_id = $sessionId;
                """;
            command.Parameters.AddWithValue("$sessionId", sessionId.ToString());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            version = reader.GetInt32(0);
            finalizedAtUtc = reader.IsDBNull(1)
                ? null
                : Parse(reader.GetString(1));
        }

        if (_afterSessionHeaderRead is not null)
        {
            await _afterSessionHeaderRead(cancellationToken);
        }

        var entries = await LoadEntriesAsync(
            connection,
            transaction,
            sessionId,
            cancellationToken);
        var auditEntries = await LoadAuditEntriesAsync(
            connection,
            transaction,
            sessionId,
            cancellationToken);
        return new AttendanceSessionRecord(
            sessionId,
            entries,
            auditEntries,
            finalizedAtUtc,
            version);
    }

    private static async Task<IReadOnlyList<AttendanceEntry>> LoadEntriesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT student_id,
                   status,
                   check_in_method,
                   received_at_utc,
                   credential_id,
                   manual_reason,
                   confirmed_by_proctor_id
            FROM attendance_entries
            WHERE session_id = $sessionId
            ORDER BY rowid;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());

        var entries = new List<AttendanceEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AttendanceEntry(
                Guid.Parse(reader.GetString(0)),
                Enum.Parse<AttendanceStatus>(reader.GetString(1)),
                reader.IsDBNull(2)
                    ? null
                    : Enum.Parse<AttendanceCheckInMethod>(reader.GetString(2)),
                reader.IsDBNull(3) ? null : Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6))));
        }

        return entries;
    }

    private static async Task<IReadOnlyList<AttendanceAuditEntry>> LoadAuditEntriesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT event_id,
                   student_id,
                   prior_status,
                   new_status,
                   reason,
                   proctor_id,
                   occurred_at_utc
            FROM attendance_audit
            WHERE session_id = $sessionId
            ORDER BY rowid;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());

        var entries = new List<AttendanceAuditEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AttendanceAuditEntry(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<AttendanceStatus>(reader.GetString(2)),
                Enum.Parse<AttendanceStatus>(reader.GetString(3)),
                reader.GetString(4),
                Guid.Parse(reader.GetString(5)),
                Parse(reader.GetString(6))));
        }

        return entries;
    }

    private async Task<AttendanceSessionRecord> LoadRequiredAsync(
        SqliteConnection connection,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction(deferred: true);
        var record = await LoadAsync(
            connection,
            transaction,
            sessionId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return record
            ?? throw new InvalidOperationException("The attendance record could not be loaded.");
    }

    private SqliteConnection CreateConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = 5
        };
        return new SqliteConnection(connectionString.ToString());
    }

    private static string Format(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }
}
