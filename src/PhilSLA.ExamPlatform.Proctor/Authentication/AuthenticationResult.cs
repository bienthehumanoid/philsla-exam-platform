namespace PhilSLA.ExamPlatform.Proctor.Authentication;

public sealed record AuthenticationResult(
    bool Succeeded,
    ProctorIdentity? Proctor)
{
    public static AuthenticationResult Failed { get; } = new(false, null);

    public static AuthenticationResult Success(ProctorIdentity proctor)
    {
        ArgumentNullException.ThrowIfNull(proctor);
        return new AuthenticationResult(true, proctor);
    }
}
