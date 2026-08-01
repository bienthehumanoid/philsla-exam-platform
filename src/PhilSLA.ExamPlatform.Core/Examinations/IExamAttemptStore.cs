namespace PhilSLA.ExamPlatform.Core.Examinations;

public interface IExamAttemptStore
{
    Task<ExamAttemptRecord?> LoadAsync(
        Guid candidateId,
        Guid examId,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> CreateAsync(
        Guid candidateId,
        ExamDefinition definition,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> AppendAnswerAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        Guid choiceId,
        DateTimeOffset savedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> SetQuestionFlagAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        bool isFlagged,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> SetCurrentQuestionAsync(
        Guid attemptId,
        int questionNumber,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> SubmitActiveBlockAsync(
        Guid attemptId,
        BlockSubmissionReason reason,
        bool completesExam,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExamAttemptRecord> StartNextBlockAsync(
        Guid attemptId,
        ExamBlockDefinition block,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default);
}
