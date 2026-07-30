namespace PhilSLA.ExamPlatform.Candidate.Authentication;

public sealed record CandidateIdentity(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? Suffix,
    string Email,
    DateOnly Birthday);
