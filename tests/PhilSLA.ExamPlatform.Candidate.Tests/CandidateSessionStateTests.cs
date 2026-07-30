using PhilSLA.ExamPlatform.Candidate.Authentication;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class CandidateSessionStateTests
{
    [TestMethod]
    public void SignInAndSignOut_UpdateAuthenticatedCandidate()
    {
        var session = new CandidateSessionState();
        var candidate = new CandidateIdentity(
            Guid.NewGuid(),
            "Demo",
            null,
            "Candidate",
            null,
            "candidate@example.test",
            new DateOnly(2000, 1, 1));

        session.SignIn(candidate);

        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreSame(candidate, session.Candidate);

        session.SignOut();

        Assert.IsFalse(session.IsAuthenticated);
        Assert.IsNull(session.Candidate);
    }
}
