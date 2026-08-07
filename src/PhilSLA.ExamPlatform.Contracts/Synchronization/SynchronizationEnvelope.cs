using PhilSLA.ExamPlatform.Contracts.Protocol;

namespace PhilSLA.ExamPlatform.Contracts.Synchronization;

public sealed record SynchronizationEnvelope(
    int ProtocolVersion,
    Guid MessageId,
    Guid AttemptId,
    Guid CandidateId,
    long SequenceNumber,
    string MessageType,
    string ContentType,
    byte[] Payload,
    string PayloadSha256Hex,
    DateTimeOffset CreatedAtUtc) : IProtocolContract;
