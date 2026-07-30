using PhilSLA.ExamPlatform.Candidate.Authentication;

namespace PhilSLA.ExamPlatform.Candidate.Persistence;

public sealed record TemporaryCandidateAccount(
    CandidateIdentity Candidate,
    string PasswordHash);
