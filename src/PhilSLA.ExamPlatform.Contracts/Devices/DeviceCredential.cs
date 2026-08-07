using PhilSLA.ExamPlatform.Contracts.Protocol;
using PhilSLA.ExamPlatform.Contracts.Security;

namespace PhilSLA.ExamPlatform.Contracts.Devices;

public sealed record DeviceCredential(
    int ProtocolVersion,
    Guid CredentialId,
    Guid DeviceId,
    string PublicKeyAlgorithm,
    string PublicKeyBase64,
    string Issuer,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    SignatureEnvelope Signature) : IProtocolContract;
