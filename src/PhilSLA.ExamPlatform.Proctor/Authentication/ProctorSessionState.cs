namespace PhilSLA.ExamPlatform.Proctor.Authentication;

public sealed class ProctorSessionState
{
    public ProctorIdentity? Proctor { get; private set; }

    public bool IsAuthenticated => Proctor is not null;

    public void SignIn(ProctorIdentity proctor)
    {
        ArgumentNullException.ThrowIfNull(proctor);
        Proctor = proctor;
    }

    public void SignOut()
    {
        Proctor = null;
    }
}
