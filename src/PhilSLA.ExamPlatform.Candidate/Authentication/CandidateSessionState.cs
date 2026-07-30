namespace PhilSLA.ExamPlatform.Candidate.Authentication;

public sealed class CandidateSessionState
{
    public CandidateIdentity? Candidate { get; private set; }

    public bool IsAuthenticated => Candidate is not null;

    public void SignIn(CandidateIdentity candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Candidate = candidate;
    }

    public void SignOut()
    {
        Candidate = null;
    }
}
