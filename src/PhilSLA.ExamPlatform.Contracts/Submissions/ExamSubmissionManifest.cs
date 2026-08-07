using PhilSLA.ExamPlatform.Contracts.Protocol;
using PhilSLA.ExamPlatform.Contracts.Security;

namespace PhilSLA.ExamPlatform.Contracts.Submissions;

public sealed record ExamSubmissionManifest(
    int ProtocolVersion,
    Guid SubmissionId,
    Guid PermitId,
    Guid AttemptId,
    Guid CandidateId,
    Guid ExamId,
    Guid PackageId,
    SubmissionCompletionReason CompletionReason,
    DateTimeOffset FinalizedAtUtc,
    int AnswerCount,
    string FinalAnswerChainSha256Hex,
    SignatureEnvelope Signature) : IProtocolContract;
