namespace PhilSLA.ExamPlatform.Proctor.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
