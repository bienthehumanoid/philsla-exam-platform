namespace PhilSLA.ExamPlatform.Candidate.Authentication;

public sealed record AuthenticationResult(
    bool Succeeded,
    CandidateIdentity? Candidate)
{
    public static AuthenticationResult Failed { get; } = new(false, null);

    public static AuthenticationResult Success(CandidateIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new AuthenticationResult(true, candidate);
    }
}
