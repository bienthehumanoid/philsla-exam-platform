using Bunit;
using LoginComponent = PhilSLA.ExamPlatform.Candidate.Components.Pages.Login;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class LoginTests
{
    [TestMethod]
    public void PasswordToggle_ChangesPresentationWithoutClearingValue()
    {
        using var context = new BunitContext();
        var component = context.Render<LoginComponent>();
        var password = component.Find("#password");

        password.Change("secret");
        component.Find(".password-toggle").Click();

        password = component.Find("#password");
        Assert.AreEqual("text", password.GetAttribute("type"));
        Assert.AreEqual("secret", password.GetAttribute("value"));
        Assert.AreEqual(
            "Hide password",
            component.Find(".password-toggle").GetAttribute("aria-label"));
    }

    [TestMethod]
    public void InvalidSubmission_ShowsRequiredFieldErrors()
    {
        using var context = new BunitContext();
        var component = context.Render<LoginComponent>();

        component.Find("button[type='submit']").Click();

        var errors = component
            .FindAll(".validation-message")
            .Select(element => element.TextContent)
            .ToArray();
        CollectionAssert.Contains(errors, "Email address is required.");
        CollectionAssert.Contains(errors, "Password is required.");
    }

    [TestMethod]
    public void ValidLocalSubmission_ShowsAuthenticationUnavailableMessage()
    {
        using var context = new BunitContext();
        var component = context.Render<LoginComponent>();

        component.Find("#email").Change("candidate@example.test");
        component.Find("#password").Change("secret");
        component.Find("button[type='submit']").Click();

        Assert.AreEqual(
            "Online authentication is not available in this build.",
            component.Find(".submission-message").TextContent.Trim());
    }
}
