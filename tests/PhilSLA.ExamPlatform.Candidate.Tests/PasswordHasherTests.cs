using PhilSLA.ExamPlatform.Candidate.Authentication;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class PasswordHasherTests
{
    [TestMethod]
    public void Hash_StoresNoPlaintextAndVerifiesOnlyMatchingPassword()
    {
        var hasher = new PasswordHasher();

        var encodedHash = hasher.Hash("DemoExam!2026");

        Assert.DoesNotContain("DemoExam!2026", encodedHash);
        Assert.IsTrue(hasher.Verify("DemoExam!2026", encodedHash));
        Assert.IsFalse(hasher.Verify("incorrect", encodedHash));
    }

    [TestMethod]
    public void Verify_ReturnsFalseForMalformedHash()
    {
        var hasher = new PasswordHasher();

        Assert.IsFalse(hasher.Verify("DemoExam!2026", "not-a-valid-hash"));
    }
}
