using PhilSLA.ExamPlatform.Proctor.Authentication;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class PasswordHasherTests
{
    [TestMethod]
    public void Hash_StoresNoPlaintextAndVerifiesOnlyMatchingPassword()
    {
        var hasher = new PasswordHasher();

        var encodedHash = hasher.Hash("DemoProctor!2026");

        Assert.DoesNotContain("DemoProctor!2026", encodedHash);
        Assert.IsTrue(hasher.Verify("DemoProctor!2026", encodedHash));
        Assert.IsFalse(hasher.Verify("incorrect", encodedHash));
    }

    [TestMethod]
    public void Verify_ReturnsFalseForMalformedHash()
    {
        var hasher = new PasswordHasher();

        Assert.IsFalse(hasher.Verify("DemoProctor!2026", "not-a-valid-hash"));
    }
}
