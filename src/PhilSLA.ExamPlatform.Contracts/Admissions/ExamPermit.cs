using PhilSLA.ExamPlatform.Contracts.Protocol;
using PhilSLA.ExamPlatform.Contracts.Security;

namespace PhilSLA.ExamPlatform.Contracts.Admissions;

public sealed record ExamPermit(
    int ProtocolVersion,
    Guid PermitId,
    Guid CandidateId,
    Guid DeviceId,
    Guid ExamId,
    Guid PackageId,
    Guid SessionId,
    string RoomCode,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ScheduledEndAtUtc,
    IReadOnlyList<string> AccommodationCodes,
    string Issuer,
    SignatureEnvelope Signature) : IProtocolContract;
