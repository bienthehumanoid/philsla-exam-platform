using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Candidate.Persistence;
using Microsoft.Data.Sqlite;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class TemporaryAuthenticationServiceTests
{
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"philsla-candidate-tests-{Guid.NewGuid():N}.db");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [TestMethod]
    public async Task AuthenticateAsync_AcceptsSeededCandidateCaseInsensitively()
    {
        var service = CreateService();

        var result = await service.AuthenticateAsync(
            "  CANDIDATE@EXAMPLE.TEST ",
            TemporaryCandidateRepository.DemoPassword);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual("Demo", result.Candidate.FirstName);
        Assert.AreEqual(TemporaryCandidateRepository.DemoEmail, result.Candidate.Email);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RejectsUnknownEmailAndWrongPassword()
    {
        var service = CreateService();

        var unknown = await service.AuthenticateAsync(
            "unknown@example.test",
            TemporaryCandidateRepository.DemoPassword);
        var wrongPassword = await service.AuthenticateAsync(
            TemporaryCandidateRepository.DemoEmail,
            "incorrect");

        Assert.IsFalse(unknown.Succeeded);
        Assert.IsNull(unknown.Candidate);
        Assert.IsFalse(wrongPassword.Succeeded);
        Assert.IsNull(wrongPassword.Candidate);
    }

    [TestMethod]
    public async Task Repository_PersistsOnlyAnEncodedPasswordHash()
    {
        var hasher = new PasswordHasher();
        var repository = new TemporaryCandidateRepository(_databasePath, hasher);

        var account = await repository.FindByEmailAsync(
            TemporaryCandidateRepository.DemoEmail);

        Assert.IsNotNull(account);
        Assert.DoesNotContain(
            TemporaryCandidateRepository.DemoPassword,
            account.PasswordHash);
        Assert.IsTrue(hasher.Verify(
            TemporaryCandidateRepository.DemoPassword,
            account.PasswordHash));
    }

    private TemporaryAuthenticationService CreateService()
    {
        var hasher = new PasswordHasher();
        var repository = new TemporaryCandidateRepository(_databasePath, hasher);
        return new TemporaryAuthenticationService(repository, hasher);
    }
}
