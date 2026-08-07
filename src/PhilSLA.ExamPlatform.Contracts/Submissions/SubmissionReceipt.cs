using PhilSLA.ExamPlatform.Contracts.Protocol;
using PhilSLA.ExamPlatform.Contracts.Security;

namespace PhilSLA.ExamPlatform.Contracts.Submissions;

public sealed record SubmissionReceipt(
    int ProtocolVersion,
    Guid ReceiptId,
    Guid SubmissionId,
    Guid AttemptId,
    Guid ExamId,
    Guid ProctorSessionId,
    string SubmissionSha256Hex,
    DateTimeOffset ReceivedAtUtc,
    string Issuer,
    SignatureEnvelope Signature) : IProtocolContract;
