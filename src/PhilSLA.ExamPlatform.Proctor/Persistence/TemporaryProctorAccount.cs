using PhilSLA.ExamPlatform.Proctor.Authentication;

namespace PhilSLA.ExamPlatform.Proctor.Persistence;

public sealed record TemporaryProctorAccount(
    ProctorIdentity Proctor,
    string PasswordHash);
