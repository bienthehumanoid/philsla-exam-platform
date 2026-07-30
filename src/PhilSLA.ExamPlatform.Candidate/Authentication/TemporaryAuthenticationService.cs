using PhilSLA.ExamPlatform.Candidate.Persistence;

namespace PhilSLA.ExamPlatform.Candidate.Authentication;

public sealed class TemporaryAuthenticationService(
    TemporaryCandidateRepository repository,
    PasswordHasher passwordHasher) : IAuthenticationService
{
    public async Task<AuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return AuthenticationResult.Failed;
        }

        var account = await repository.FindByEmailAsync(
            email,
            cancellationToken);

        if (account is null ||
            !passwordHasher.Verify(password, account.PasswordHash))
        {
            return AuthenticationResult.Failed;
        }

        return AuthenticationResult.Success(account.Candidate);
    }
}
