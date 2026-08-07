namespace PhilSLA.ExamPlatform.Candidate.Devices;

public sealed record LocalDeviceIdentity(
    Guid DeviceId,
    string PublicKeyAlgorithm,
    string PublicKeyBase64,
    string PublicKeyThumbprintSha256Hex,
    DateTimeOffset CreatedAtUtc);
