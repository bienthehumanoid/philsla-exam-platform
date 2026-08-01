namespace PhilSLA.ExamPlatform.Core.Examinations;

public enum ExamAttemptStatus
{
    Active,
    AwaitingNextBlock,
    Completed
}

public enum BlockAttemptStatus
{
    Active,
    Submitted,
    TimedOut
}

public enum BlockSubmissionReason
{
    Candidate,
    TimeExpired
}

public sealed record ExamAttemptRecord(
    Guid Id,
    Guid CandidateId,
    Guid ExamId,
    ExamAttemptStatus Status,
    int ActiveBlockNumber,
    int CurrentQuestionNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<BlockAttemptRecord> Blocks,
    IReadOnlyDictionary<Guid, AnswerSelectionRecord> Answers,
    IReadOnlySet<Guid> FlaggedQuestionIds);

public sealed record BlockAttemptRecord(
    Guid Id,
    Guid BlockId,
    int BlockNumber,
    BlockAttemptStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    BlockSubmissionReason? SubmissionReason);

public sealed record AnswerSelectionRecord(
    Guid QuestionId,
    Guid ChoiceId,
    int RevisionNumber,
    DateTimeOffset SavedAtUtc,
    string IntegrityHash);
