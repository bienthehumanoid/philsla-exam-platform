using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Examinations;
using PhilSLA.ExamPlatform.Infrastructure.Examinations;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class SqliteExamAttemptStoreTests
{
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"philsla-exam-tests-{Guid.NewGuid():N}.db");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfPresent(_databasePath);
        DeleteIfPresent($"{_databasePath}-wal");
        DeleteIfPresent($"{_databasePath}-shm");
    }

    [TestMethod]
    public async Task AnswerRevisions_SurviveStoreRestartWithLatestSelection()
    {
        var candidateId = Guid.NewGuid();
        var startedAt = new DateTimeOffset(
            2026,
            8,
            15,
            8,
            0,
            0,
            TimeSpan.Zero);
        var firstStore = new SqliteExamAttemptStore(_databasePath);
        var attempt = await firstStore.CreateAsync(
            candidateId,
            ExamTestData.Definition,
            startedAt);
        var block = ExamTestData.Definition.Blocks[0];
        var question = block.Questions[0];

        await firstStore.AppendAnswerAsync(
            attempt.Id,
            block.Id,
            question.Id,
            question.Choices[0].Id,
            startedAt.AddMinutes(1));
        await firstStore.AppendAnswerAsync(
            attempt.Id,
            block.Id,
            question.Id,
            question.Choices[1].Id,
            startedAt.AddMinutes(2));

        var restartedStore = new SqliteExamAttemptStore(_databasePath);
        var restored = await restartedStore.LoadAsync(
            candidateId,
            ExamTestData.Definition.Id);

        Assert.IsNotNull(restored);
        Assert.AreEqual(
            question.Choices[1].Id,
            restored.Answers[question.Id].ChoiceId);
        Assert.AreEqual(
            2,
            restored.Answers[question.Id].RevisionNumber);
        Assert.HasCount(
            64,
            restored.Answers[question.Id].IntegrityHash);
        Assert.AreEqual(
            startedAt.AddHours(1),
            restored.Blocks[0].DeadlineAtUtc);
    }

    [TestMethod]
    public async Task SubmittedBlock_IsRestoredAsReadOnlyTransitionState()
    {
        var candidateId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var store = new SqliteExamAttemptStore(_databasePath);
        var attempt = await store.CreateAsync(
            candidateId,
            ExamTestData.Definition,
            startedAt);

        var submitted = await store.SubmitActiveBlockAsync(
            attempt.Id,
            BlockSubmissionReason.Candidate,
            completesExam: false,
            startedAt.AddMinutes(10));

        Assert.AreEqual(
            ExamAttemptStatus.AwaitingNextBlock,
            submitted.Status);
        Assert.AreEqual(
            BlockAttemptStatus.Submitted,
            submitted.Blocks[0].Status);
        Assert.AreEqual(
            BlockSubmissionReason.Candidate,
            submitted.Blocks[0].SubmissionReason);
    }

    [TestMethod]
    public async Task ModifiedAnswerRevision_IsRejectedDuringRecovery()
    {
        var candidateId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var store = new SqliteExamAttemptStore(_databasePath);
        var attempt = await store.CreateAsync(
            candidateId,
            ExamTestData.Definition,
            startedAt);
        var block = ExamTestData.Definition.Blocks[0];
        var question = block.Questions[0];
        await store.AppendAnswerAsync(
            attempt.Id,
            block.Id,
            question.Id,
            question.Choices[0].Id,
            startedAt.AddMinutes(1));

        await using (var connection = new SqliteConnection(
            $"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE exam_answer_revisions
                SET choice_id = $choiceId
                WHERE attempt_id = $attemptId;
                """;
            command.Parameters.AddWithValue(
                "$choiceId",
                Guid.NewGuid().ToString());
            command.Parameters.AddWithValue(
                "$attemptId",
                attempt.Id.ToString());
            await command.ExecuteNonQueryAsync();
        }

        var restartedStore = new SqliteExamAttemptStore(_databasePath);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => restartedStore.LoadAsync(
                candidateId,
                ExamTestData.Definition.Id));
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
