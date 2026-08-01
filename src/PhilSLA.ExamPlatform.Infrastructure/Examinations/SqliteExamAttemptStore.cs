using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Examinations;

namespace PhilSLA.ExamPlatform.Infrastructure.Examinations;

public sealed class SqliteExamAttemptStore(string databasePath)
    : IExamAttemptStore
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task<ExamAttemptRecord?> LoadAsync(
        Guid candidateId,
        Guid examId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await LoadAsync(
            connection,
            candidateId,
            examId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> CreateAsync(
        Guid candidateId,
        ExamDefinition definition,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var firstBlock = definition.Blocks.Single(block => block.Number == 1);
        var attemptId = Guid.NewGuid();
        var blockAttemptId = Guid.NewGuid();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var attemptCommand = connection.CreateCommand())
        {
            attemptCommand.Transaction = transaction;
            attemptCommand.CommandText =
                """
                INSERT INTO exam_attempts (
                    id,
                    candidate_id,
                    exam_id,
                    status,
                    active_block_number,
                    current_question_number,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (
                    $id,
                    $candidateId,
                    $examId,
                    $status,
                    1,
                    1,
                    $createdAt,
                    $updatedAt
                );
                """;
            attemptCommand.Parameters.AddWithValue("$id", attemptId.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$candidateId",
                candidateId.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$examId",
                definition.Id.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$status",
                ExamAttemptStatus.Active.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$createdAt",
                Format(startedAtUtc));
            attemptCommand.Parameters.AddWithValue(
                "$updatedAt",
                Format(startedAtUtc));
            await attemptCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var blockCommand = connection.CreateCommand())
        {
            blockCommand.Transaction = transaction;
            blockCommand.CommandText =
                """
                INSERT INTO exam_block_attempts (
                    id,
                    attempt_id,
                    block_id,
                    block_number,
                    status,
                    started_at_utc,
                    deadline_at_utc
                )
                VALUES (
                    $id,
                    $attemptId,
                    $blockId,
                    1,
                    $status,
                    $startedAt,
                    $deadlineAt
                );
                """;
            blockCommand.Parameters.AddWithValue(
                "$id",
                blockAttemptId.ToString());
            blockCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            blockCommand.Parameters.AddWithValue(
                "$blockId",
                firstBlock.Id.ToString());
            blockCommand.Parameters.AddWithValue(
                "$status",
                BlockAttemptStatus.Active.ToString());
            blockCommand.Parameters.AddWithValue(
                "$startedAt",
                Format(startedAtUtc));
            blockCommand.Parameters.AddWithValue(
                "$deadlineAt",
                Format(startedAtUtc.Add(firstBlock.Duration)));
            await blockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            attemptId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> AppendAnswerAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        Guid choiceId,
        DateTimeOffset savedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        int revisionNumber;
        string? previousHash;
        await using (var revisionCommand = connection.CreateCommand())
        {
            revisionCommand.Transaction = transaction;
            revisionCommand.CommandText =
                """
                SELECT revision_number, integrity_hash
                FROM exam_answer_revisions
                WHERE attempt_id = $attemptId
                  AND question_id = $questionId
                ORDER BY revision_number DESC
                LIMIT 1;
                """;
            revisionCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            revisionCommand.Parameters.AddWithValue(
                "$questionId",
                questionId.ToString());
            await using var reader =
                await revisionCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                revisionNumber = reader.GetInt32(0) + 1;
                previousHash = reader.GetString(1);
            }
            else
            {
                revisionNumber = 1;
                previousHash = null;
            }
        }

        var integrityHash = ComputeIntegrityHash(
            attemptId,
            blockId,
            questionId,
            choiceId,
            revisionNumber,
            savedAtUtc,
            previousHash);

        await using (var answerCommand = connection.CreateCommand())
        {
            answerCommand.Transaction = transaction;
            answerCommand.CommandText =
                """
                INSERT INTO exam_answer_revisions (
                    id,
                    attempt_id,
                    block_id,
                    question_id,
                    choice_id,
                    revision_number,
                    saved_at_utc,
                    previous_hash,
                    integrity_hash
                )
                VALUES (
                    $id,
                    $attemptId,
                    $blockId,
                    $questionId,
                    $choiceId,
                    $revisionNumber,
                    $savedAt,
                    $previousHash,
                    $integrityHash
                );
                """;
            answerCommand.Parameters.AddWithValue(
                "$id",
                Guid.NewGuid().ToString());
            answerCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            answerCommand.Parameters.AddWithValue(
                "$blockId",
                blockId.ToString());
            answerCommand.Parameters.AddWithValue(
                "$questionId",
                questionId.ToString());
            answerCommand.Parameters.AddWithValue(
                "$choiceId",
                choiceId.ToString());
            answerCommand.Parameters.AddWithValue(
                "$revisionNumber",
                revisionNumber);
            answerCommand.Parameters.AddWithValue(
                "$savedAt",
                Format(savedAtUtc));
            answerCommand.Parameters.AddWithValue(
                "$previousHash",
                (object?)previousHash ?? DBNull.Value);
            answerCommand.Parameters.AddWithValue(
                "$integrityHash",
                integrityHash);
            await answerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await TouchAttemptAsync(
            connection,
            transaction,
            attemptId,
            savedAtUtc,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            attemptId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> SetQuestionFlagAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        bool isFlagged,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO exam_question_flags (
                    attempt_id,
                    block_id,
                    question_id,
                    is_flagged,
                    updated_at_utc
                )
                VALUES (
                    $attemptId,
                    $blockId,
                    $questionId,
                    $isFlagged,
                    $updatedAt
                )
                ON CONFLICT(attempt_id, question_id)
                DO UPDATE SET
                    is_flagged = excluded.is_flagged,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            command.Parameters.AddWithValue(
                "$blockId",
                blockId.ToString());
            command.Parameters.AddWithValue(
                "$questionId",
                questionId.ToString());
            command.Parameters.AddWithValue(
                "$isFlagged",
                isFlagged ? 1 : 0);
            command.Parameters.AddWithValue(
                "$updatedAt",
                Format(updatedAtUtc));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await TouchAttemptAsync(
            connection,
            transaction,
            attemptId,
            updatedAtUtc,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            attemptId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> SetCurrentQuestionAsync(
        Guid attemptId,
        int questionNumber,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE exam_attempts
            SET current_question_number = $questionNumber,
                updated_at_utc = $updatedAt
            WHERE id = $attemptId
              AND status = $activeStatus;
            """;
        command.Parameters.AddWithValue("$questionNumber", questionNumber);
        command.Parameters.AddWithValue("$updatedAt", Format(updatedAtUtc));
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());
        command.Parameters.AddWithValue(
            "$activeStatus",
            ExamAttemptStatus.Active.ToString());
        EnsureSingleWrite(
            await command.ExecuteNonQueryAsync(cancellationToken),
            "The active question could not be changed.");

        return await LoadRequiredAsync(
            connection,
            attemptId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> SubmitActiveBlockAsync(
        Guid attemptId,
        BlockSubmissionReason reason,
        bool completesExam,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var blockCommand = connection.CreateCommand())
        {
            blockCommand.Transaction = transaction;
            blockCommand.CommandText =
                """
                UPDATE exam_block_attempts
                SET status = $status,
                    submitted_at_utc = $submittedAt,
                    submission_reason = $reason
                WHERE attempt_id = $attemptId
                  AND status = $activeStatus;
                """;
            blockCommand.Parameters.AddWithValue(
                "$status",
                reason == BlockSubmissionReason.TimeExpired
                    ? BlockAttemptStatus.TimedOut.ToString()
                    : BlockAttemptStatus.Submitted.ToString());
            blockCommand.Parameters.AddWithValue(
                "$submittedAt",
                Format(submittedAtUtc));
            blockCommand.Parameters.AddWithValue(
                "$reason",
                reason.ToString());
            blockCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            blockCommand.Parameters.AddWithValue(
                "$activeStatus",
                BlockAttemptStatus.Active.ToString());
            EnsureSingleWrite(
                await blockCommand.ExecuteNonQueryAsync(cancellationToken),
                "The active block could not be submitted.");
        }

        await using (var attemptCommand = connection.CreateCommand())
        {
            attemptCommand.Transaction = transaction;
            attemptCommand.CommandText =
                """
                UPDATE exam_attempts
                SET status = $status,
                    updated_at_utc = $updatedAt
                WHERE id = $attemptId
                  AND status = $activeStatus;
                """;
            attemptCommand.Parameters.AddWithValue(
                "$status",
                completesExam
                    ? ExamAttemptStatus.Completed.ToString()
                    : ExamAttemptStatus.AwaitingNextBlock.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$updatedAt",
                Format(submittedAtUtc));
            attemptCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$activeStatus",
                ExamAttemptStatus.Active.ToString());
            EnsureSingleWrite(
                await attemptCommand.ExecuteNonQueryAsync(cancellationToken),
                "The examination attempt could not be updated.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            attemptId,
            cancellationToken);
    }

    public async Task<ExamAttemptRecord> StartNextBlockAsync(
        Guid attemptId,
        ExamBlockDefinition block,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var blockCommand = connection.CreateCommand())
        {
            blockCommand.Transaction = transaction;
            blockCommand.CommandText =
                """
                INSERT INTO exam_block_attempts (
                    id,
                    attempt_id,
                    block_id,
                    block_number,
                    status,
                    started_at_utc,
                    deadline_at_utc
                )
                VALUES (
                    $id,
                    $attemptId,
                    $blockId,
                    $blockNumber,
                    $status,
                    $startedAt,
                    $deadlineAt
                );
                """;
            blockCommand.Parameters.AddWithValue(
                "$id",
                Guid.NewGuid().ToString());
            blockCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            blockCommand.Parameters.AddWithValue(
                "$blockId",
                block.Id.ToString());
            blockCommand.Parameters.AddWithValue(
                "$blockNumber",
                block.Number);
            blockCommand.Parameters.AddWithValue(
                "$status",
                BlockAttemptStatus.Active.ToString());
            blockCommand.Parameters.AddWithValue(
                "$startedAt",
                Format(startedAtUtc));
            blockCommand.Parameters.AddWithValue(
                "$deadlineAt",
                Format(startedAtUtc.Add(block.Duration)));
            await blockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var attemptCommand = connection.CreateCommand())
        {
            attemptCommand.Transaction = transaction;
            attemptCommand.CommandText =
                """
                UPDATE exam_attempts
                SET status = $status,
                    active_block_number = $blockNumber,
                    current_question_number = 1,
                    updated_at_utc = $updatedAt
                WHERE id = $attemptId
                  AND status = $waitingStatus;
                """;
            attemptCommand.Parameters.AddWithValue(
                "$status",
                ExamAttemptStatus.Active.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$blockNumber",
                block.Number);
            attemptCommand.Parameters.AddWithValue(
                "$updatedAt",
                Format(startedAtUtc));
            attemptCommand.Parameters.AddWithValue(
                "$attemptId",
                attemptId.ToString());
            attemptCommand.Parameters.AddWithValue(
                "$waitingStatus",
                ExamAttemptStatus.AwaitingNextBlock.ToString());
            EnsureSingleWrite(
                await attemptCommand.ExecuteNonQueryAsync(cancellationToken),
                "The next block could not be started.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(
            connection,
            attemptId,
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

                CREATE TABLE IF NOT EXISTS exam_attempts (
                    id TEXT PRIMARY KEY,
                    candidate_id TEXT NOT NULL,
                    exam_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    active_block_number INTEGER NOT NULL,
                    current_question_number INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    UNIQUE(candidate_id, exam_id)
                );

                CREATE TABLE IF NOT EXISTS exam_block_attempts (
                    id TEXT PRIMARY KEY,
                    attempt_id TEXT NOT NULL,
                    block_id TEXT NOT NULL,
                    block_number INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    started_at_utc TEXT NOT NULL,
                    deadline_at_utc TEXT NOT NULL,
                    submitted_at_utc TEXT,
                    submission_reason TEXT,
                    UNIQUE(attempt_id, block_number),
                    FOREIGN KEY(attempt_id) REFERENCES exam_attempts(id)
                );

                CREATE TABLE IF NOT EXISTS exam_answer_revisions (
                    id TEXT PRIMARY KEY,
                    attempt_id TEXT NOT NULL,
                    block_id TEXT NOT NULL,
                    question_id TEXT NOT NULL,
                    choice_id TEXT NOT NULL,
                    revision_number INTEGER NOT NULL,
                    saved_at_utc TEXT NOT NULL,
                    previous_hash TEXT,
                    integrity_hash TEXT NOT NULL,
                    UNIQUE(attempt_id, question_id, revision_number),
                    FOREIGN KEY(attempt_id) REFERENCES exam_attempts(id)
                );

                CREATE TABLE IF NOT EXISTS exam_question_flags (
                    attempt_id TEXT NOT NULL,
                    block_id TEXT NOT NULL,
                    question_id TEXT NOT NULL,
                    is_flagged INTEGER NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY(attempt_id, question_id),
                    FOREIGN KEY(attempt_id) REFERENCES exam_attempts(id)
                );

                CREATE INDEX IF NOT EXISTS ix_exam_answer_revisions_latest
                    ON exam_answer_revisions(
                        attempt_id,
                        question_id,
                        revision_number DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<ExamAttemptRecord?> LoadAsync(
        SqliteConnection connection,
        Guid candidateId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id,
                   status,
                   active_block_number,
                   current_question_number,
                   created_at_utc,
                   updated_at_utc
            FROM exam_attempts
            WHERE candidate_id = $candidateId
              AND exam_id = $examId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "$candidateId",
            candidateId.ToString());
        command.Parameters.AddWithValue("$examId", examId.ToString());

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var attemptId = Guid.Parse(reader.GetString(0));
        var status = Enum.Parse<ExamAttemptStatus>(reader.GetString(1));
        var activeBlockNumber = reader.GetInt32(2);
        var currentQuestionNumber = reader.GetInt32(3);
        var createdAtUtc = Parse(reader.GetString(4));
        var updatedAtUtc = Parse(reader.GetString(5));
        await reader.DisposeAsync();

        var blocks = await LoadBlocksAsync(
            connection,
            attemptId,
            cancellationToken);
        var answers = await LoadLatestAnswersAsync(
            connection,
            attemptId,
            cancellationToken);
        var flags = await LoadFlagsAsync(
            connection,
            attemptId,
            cancellationToken);

        return new ExamAttemptRecord(
            attemptId,
            candidateId,
            examId,
            status,
            activeBlockNumber,
            currentQuestionNumber,
            createdAtUtc,
            updatedAtUtc,
            blocks,
            answers,
            flags);
    }

    private async Task<ExamAttemptRecord> LoadRequiredAsync(
        SqliteConnection connection,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT candidate_id, exam_id
            FROM exam_attempts
            WHERE id = $attemptId;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The examination attempt no longer exists.");
        }

        var candidateId = Guid.Parse(reader.GetString(0));
        var examId = Guid.Parse(reader.GetString(1));
        await reader.DisposeAsync();

        return await LoadAsync(
            connection,
            candidateId,
            examId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The examination attempt could not be reloaded.");
    }

    private static async Task<IReadOnlyList<BlockAttemptRecord>> LoadBlocksAsync(
        SqliteConnection connection,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id,
                   block_id,
                   block_number,
                   status,
                   started_at_utc,
                   deadline_at_utc,
                   submitted_at_utc,
                   submission_reason
            FROM exam_block_attempts
            WHERE attempt_id = $attemptId
            ORDER BY block_number;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());

        var blocks = new List<BlockAttemptRecord>();
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            blocks.Add(new BlockAttemptRecord(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                Enum.Parse<BlockAttemptStatus>(reader.GetString(3)),
                Parse(reader.GetString(4)),
                Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : Parse(reader.GetString(6)),
                reader.IsDBNull(7)
                    ? null
                    : Enum.Parse<BlockSubmissionReason>(
                        reader.GetString(7))));
        }

        return blocks;
    }

    private static async Task<IReadOnlyDictionary<Guid, AnswerSelectionRecord>>
        LoadLatestAnswersAsync(
            SqliteConnection connection,
            Guid attemptId,
            CancellationToken cancellationToken)
    {
        await ValidateAnswerRevisionChainsAsync(
            connection,
            attemptId,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT revisions.question_id,
                   revisions.choice_id,
                   revisions.revision_number,
                   revisions.saved_at_utc,
                   revisions.integrity_hash
            FROM exam_answer_revisions AS revisions
            WHERE revisions.attempt_id = $attemptId
              AND NOT EXISTS (
                  SELECT 1
                  FROM exam_answer_revisions AS newer
                  WHERE newer.attempt_id = revisions.attempt_id
                    AND newer.question_id = revisions.question_id
                    AND newer.revision_number > revisions.revision_number
              );
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());

        var answers = new Dictionary<Guid, AnswerSelectionRecord>();
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var questionId = Guid.Parse(reader.GetString(0));
            answers[questionId] = new AnswerSelectionRecord(
                questionId,
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                Parse(reader.GetString(3)),
                reader.GetString(4));
        }

        return answers;
    }

    private static async Task ValidateAnswerRevisionChainsAsync(
        SqliteConnection connection,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT block_id,
                   question_id,
                   choice_id,
                   revision_number,
                   saved_at_utc,
                   previous_hash,
                   integrity_hash
            FROM exam_answer_revisions
            WHERE attempt_id = $attemptId
            ORDER BY question_id, revision_number;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());

        Guid? currentQuestionId = null;
        var expectedRevision = 1;
        string? expectedPreviousHash = null;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var blockId = Guid.Parse(reader.GetString(0));
            var questionId = Guid.Parse(reader.GetString(1));
            var choiceId = Guid.Parse(reader.GetString(2));
            var revisionNumber = reader.GetInt32(3);
            var savedAtUtc = Parse(reader.GetString(4));
            var previousHash =
                reader.IsDBNull(5) ? null : reader.GetString(5);
            var integrityHash = reader.GetString(6);

            if (currentQuestionId != questionId)
            {
                currentQuestionId = questionId;
                expectedRevision = 1;
                expectedPreviousHash = null;
            }

            var computedHash = ComputeIntegrityHash(
                attemptId,
                blockId,
                questionId,
                choiceId,
                revisionNumber,
                savedAtUtc,
                previousHash);
            if (revisionNumber != expectedRevision ||
                previousHash != expectedPreviousHash ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(integrityHash),
                    Convert.FromHexString(computedHash)))
            {
                throw new InvalidDataException(
                    "An examination answer revision chain is invalid.");
            }

            expectedRevision++;
            expectedPreviousHash = integrityHash;
        }
    }

    private static async Task<IReadOnlySet<Guid>> LoadFlagsAsync(
        SqliteConnection connection,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT question_id
            FROM exam_question_flags
            WHERE attempt_id = $attemptId
              AND is_flagged = 1;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());

        var flags = new HashSet<Guid>();
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            flags.Add(Guid.Parse(reader.GetString(0)));
        }

        return flags;
    }

    private static async Task TouchAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid attemptId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE exam_attempts
            SET updated_at_utc = $updatedAt
            WHERE id = $attemptId
              AND status = $activeStatus;
            """;
        command.Parameters.AddWithValue(
            "$updatedAt",
            Format(updatedAtUtc));
        command.Parameters.AddWithValue("$attemptId", attemptId.ToString());
        command.Parameters.AddWithValue(
            "$activeStatus",
            ExamAttemptStatus.Active.ToString());
        EnsureSingleWrite(
            await command.ExecuteNonQueryAsync(cancellationToken),
            "The active examination attempt could not be updated.");
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

    private static void EnsureSingleWrite(int affectedRows, string message)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(message);
        }
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

    private static string ComputeIntegrityHash(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        Guid choiceId,
        int revisionNumber,
        DateTimeOffset savedAtUtc,
        string? previousHash)
    {
        var content = string.Join(
            "|",
            attemptId,
            blockId,
            questionId,
            choiceId,
            revisionNumber.ToString(CultureInfo.InvariantCulture),
            Format(savedAtUtc),
            previousHash ?? string.Empty);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
