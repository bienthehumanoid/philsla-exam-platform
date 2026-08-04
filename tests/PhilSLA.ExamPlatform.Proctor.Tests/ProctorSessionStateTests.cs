using PhilSLA.ExamPlatform.Proctor.Authentication;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class ProctorSessionStateTests
{
    [TestMethod]
    public void SignInAndSignOut_UpdateAuthenticatedProctor()
    {
        var session = new ProctorSessionState();
        var proctor = new ProctorIdentity(
            Guid.NewGuid(),
            "Demo",
            "Proctor",
            "proctor@example.test",
            "PROCTOR");

        session.SignIn(proctor);

        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreSame(proctor, session.Proctor);

        session.SignOut();

        Assert.IsFalse(session.IsAuthenticated);
        Assert.IsNull(session.Proctor);
    }
}
