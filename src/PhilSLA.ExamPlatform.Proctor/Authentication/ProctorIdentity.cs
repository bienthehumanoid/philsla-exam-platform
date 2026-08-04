namespace PhilSLA.ExamPlatform.Proctor.Authentication;

public sealed record ProctorIdentity(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role);
