using System.Globalization;
using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Proctor.Authentication;

namespace PhilSLA.ExamPlatform.Proctor.Persistence;

public sealed class TemporaryProctorRepository(
    string databasePath,
    PasswordHasher passwordHasher)
{
    public const string DemoEmail = "proctor@example.test";
    public const string DemoPassword = "DemoProctor!2026";
    public const string DemoRole = "PROCTOR";

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task<TemporaryProctorAccount?> FindByEmailAsync(
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
                   last_name,
                   email,
                   role,
                   password_hash
            FROM temporary_proctor_users
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

        var proctor = new ProctorIdentity(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));

        return new TemporaryProctorAccount(proctor, reader.GetString(5));
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
                CREATE TABLE IF NOT EXISTS temporary_proctor_users (
                    id TEXT PRIMARY KEY,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT NOT NULL,
                    normalized_email TEXT NOT NULL UNIQUE,
                    role TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT
                );
                """;
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var seedCommand = connection.CreateCommand();
            seedCommand.CommandText =
                """
                INSERT OR IGNORE INTO temporary_proctor_users (
                    id,
                    first_name,
                    last_name,
                    email,
                    normalized_email,
                    role,
                    password_hash,
                    created_at
                )
                VALUES (
                    $id,
                    $firstName,
                    $lastName,
                    $email,
                    $normalizedEmail,
                    $role,
                    $passwordHash,
                    $createdAt
                );
                """;
            seedCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            seedCommand.Parameters.AddWithValue("$firstName", "Demo");
            seedCommand.Parameters.AddWithValue("$lastName", "Proctor");
            seedCommand.Parameters.AddWithValue("$email", DemoEmail);
            seedCommand.Parameters.AddWithValue(
                "$normalizedEmail",
                NormalizeEmail(DemoEmail));
            seedCommand.Parameters.AddWithValue("$role", DemoRole);
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
