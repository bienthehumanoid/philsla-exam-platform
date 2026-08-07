namespace PhilSLA.ExamPlatform.Contracts.Security;

public sealed record SignatureEnvelope(
    string Algorithm,
    string KeyId,
    string ValueBase64);
