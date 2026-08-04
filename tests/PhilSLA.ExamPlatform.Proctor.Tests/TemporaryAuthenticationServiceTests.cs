using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using PhilSLA.ExamPlatform.Proctor.Persistence;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class TemporaryAuthenticationServiceTests
{
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"philsla-proctor-tests-{Guid.NewGuid():N}.db");
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
    public async Task AuthenticateAsync_AcceptsSeededProctorCaseInsensitively()
    {
        var service = CreateService();

        var result = await service.AuthenticateAsync(
            "  PROCTOR@EXAMPLE.TEST ",
            TemporaryProctorRepository.DemoPassword);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Proctor);
        Assert.AreEqual("Demo", result.Proctor.FirstName);
        Assert.AreEqual(TemporaryProctorRepository.DemoEmail, result.Proctor.Email);
        Assert.AreEqual(TemporaryProctorRepository.DemoRole, result.Proctor.Role);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RejectsUnknownEmailAndWrongPassword()
    {
        var service = CreateService();

        var unknown = await service.AuthenticateAsync(
            "unknown@example.test",
            TemporaryProctorRepository.DemoPassword);
        var wrongPassword = await service.AuthenticateAsync(
            TemporaryProctorRepository.DemoEmail,
            "incorrect");

        Assert.IsFalse(unknown.Succeeded);
        Assert.IsNull(unknown.Proctor);
        Assert.IsFalse(wrongPassword.Succeeded);
        Assert.IsNull(wrongPassword.Proctor);
    }

    [TestMethod]
    public async Task Repository_PersistsOnlyAnEncodedPasswordHash()
    {
        var hasher = new PasswordHasher();
        var repository = new TemporaryProctorRepository(_databasePath, hasher);

        var account = await repository.FindByEmailAsync(
            TemporaryProctorRepository.DemoEmail);

        Assert.IsNotNull(account);
        Assert.DoesNotContain(
            TemporaryProctorRepository.DemoPassword,
            account.PasswordHash);
        Assert.IsTrue(hasher.Verify(
            TemporaryProctorRepository.DemoPassword,
            account.PasswordHash));
    }

    private TemporaryAuthenticationService CreateService()
    {
        var hasher = new PasswordHasher();
        var repository = new TemporaryProctorRepository(_databasePath, hasher);
        return new TemporaryAuthenticationService(repository, hasher);
    }
}
