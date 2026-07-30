using System.Globalization;
using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Candidate.Authentication;

namespace PhilSLA.ExamPlatform.Candidate.Persistence;

public sealed class TemporaryCandidateRepository(
    string databasePath,
    PasswordHasher passwordHasher)
{
    public const string DemoEmail = "candidate@example.test";
    public const string DemoPassword = "DemoExam!2026";

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task<TemporaryCandidateAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id,
                   first_name,
                   middle_name,
                   last_name,
                   suffix,
                   email,
                   birthday,
                   password_hash
            FROM temporary_candidate_users
            WHERE normalized_email = $normalizedEmail
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "$normalizedEmail",
            NormalizeEmail(email));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var candidate = new CandidateIdentity(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            DateOnly.ParseExact(
                reader.GetString(6),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture));

        return new TemporaryCandidateAccount(candidate, reader.GetString(7));
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

            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS temporary_candidate_users (
                    id TEXT PRIMARY KEY,
                    first_name TEXT NOT NULL,
                    middle_name TEXT,
                    last_name TEXT NOT NULL,
                    suffix TEXT,
                    email TEXT NOT NULL,
                    normalized_email TEXT NOT NULL UNIQUE,
                    birthday TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT
                );
                """;
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var seedCommand = connection.CreateCommand();
            seedCommand.CommandText =
                """
                INSERT OR IGNORE INTO temporary_candidate_users (
                    id,
                    first_name,
                    middle_name,
                    last_name,
                    suffix,
                    email,
                    normalized_email,
                    birthday,
                    password_hash,
                    created_at
                )
                VALUES (
                    $id,
                    $firstName,
                    NULL,
                    $lastName,
                    NULL,
                    $email,
                    $normalizedEmail,
                    $birthday,
                    $passwordHash,
                    $createdAt
                );
                """;
            seedCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            seedCommand.Parameters.AddWithValue("$firstName", "Demo");
            seedCommand.Parameters.AddWithValue("$lastName", "Candidate");
            seedCommand.Parameters.AddWithValue("$email", DemoEmail);
            seedCommand.Parameters.AddWithValue(
                "$normalizedEmail",
                NormalizeEmail(DemoEmail));
            seedCommand.Parameters.AddWithValue("$birthday", "2000-01-01");
            seedCommand.Parameters.AddWithValue(
                "$passwordHash",
                passwordHasher.Hash(DemoPassword));
            seedCommand.Parameters.AddWithValue(
                "$createdAt",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await seedCommand.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private SqliteConnection CreateConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        return new SqliteConnection(connectionString.ToString());
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }
}
