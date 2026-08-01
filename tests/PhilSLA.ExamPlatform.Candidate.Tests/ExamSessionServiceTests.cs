using PhilSLA.ExamPlatform.Core.Examinations;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamSessionServiceTests
{
    private static readonly Guid CandidateId = Guid.NewGuid();
    private static readonly DateTimeOffset StartTime =
        new(2026, 8, 15, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Start_IsIdempotentAndDoesNotResetDeadline()
    {
        var (service, _, time) = CreateService();

        await service.StartAsync(CandidateId);
        time.Advance(TimeSpan.FromMinutes(5));
        var restarted = await service.StartAsync(CandidateId);

        Assert.AreEqual(
            TimeSpan.FromMinutes(55),
            restarted.RemainingTime);
    }

    [TestMethod]
    public async Task AnswerChanges_CreateAppendOnlyRevisions()
    {
        var (service, store, _) = CreateService();
        var started = await service.StartAsync(CandidateId);
        var question = started.CurrentQuestion;

        await service.SelectAnswerAsync(
            question.Id,
            question.Choices[0].Id);
        var changed = await service.SelectAnswerAsync(
            question.Id,
            question.Choices[1].Id);

        Assert.AreEqual(2, store.AnswerRevisionCount);
        Assert.AreEqual(
            2,
            changed.Attempt.Answers[question.Id].RevisionNumber);
        Assert.AreEqual(
            question.Choices[1].Id,
            changed.Attempt.Answers[question.Id].ChoiceId);
    }

    [TestMethod]
    public async Task FinishingBlock_WaitsForExplicitNextBlockStart()
    {
        var (service, _, time) = CreateService();
        await service.StartAsync(CandidateId);

        var submitted = await service.SubmitActiveBlockAsync(
            BlockSubmissionReason.Candidate);
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.AreEqual(
            ExamAttemptStatus.AwaitingNextBlock,
            submitted.Attempt.Status);

        var next = await service.StartNextBlockAsync();

        Assert.AreEqual(ExamAttemptStatus.Active, next.Attempt.Status);
        Assert.AreEqual(2, next.Attempt.ActiveBlockNumber);
        Assert.AreEqual(TimeSpan.FromMinutes(60), next.RemainingTime);
    }

    [TestMethod]
    public async Task ExpiredBlock_IsAutomaticallySubmitted()
    {
        var (service, _, time) = CreateService();
        await service.StartAsync(CandidateId);
        time.Advance(TimeSpan.FromMinutes(60));

        var expired = await service.CheckTimeoutAsync();

        Assert.AreEqual(
            ExamAttemptStatus.AwaitingNextBlock,
            expired.Attempt.Status);
        Assert.AreEqual(
            BlockAttemptStatus.TimedOut,
            expired.ActiveBlockAttempt.Status);
        Assert.AreEqual(
            BlockSubmissionReason.TimeExpired,
            expired.ActiveBlockAttempt.SubmissionReason);
    }

    [TestMethod]
    public async Task ChoiceFromAnotherQuestion_IsRejected()
    {
        var (service, _, _) = CreateService();
        var started = await service.StartAsync(CandidateId);
        var current = started.CurrentQuestion;
        var otherChoice = started.ActiveBlock.Questions[1].Choices[0];

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.SelectAnswerAsync(current.Id, otherChoice.Id));
    }

    private static (
        ExamSessionService Service,
        InMemoryExamAttemptStore Store,
        TestTimeProvider Time) CreateService()
    {
        var store = new InMemoryExamAttemptStore();
        var time = new TestTimeProvider(StartTime);
        return (
            new ExamSessionService(
                new TestExamDefinitionProvider(),
                store,
                time),
            store,
            time);
    }
}
