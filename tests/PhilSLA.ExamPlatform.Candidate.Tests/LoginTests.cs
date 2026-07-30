using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using LoginComponent = PhilSLA.ExamPlatform.Candidate.Components.Pages.Login;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class LoginTests
{
    [TestMethod]
    public void PasswordToggle_ChangesPresentationWithoutClearingValue()
    {
        using var context = CreateContext();
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
        Assert.AreEqual(
            string.Empty,
            component.Find(".password-toggle").TextContent.Trim());
        Assert.HasCount(1, component.FindAll(".password-toggle svg"));
    }

    [TestMethod]
    public void InvalidSubmission_ShowsRequiredFieldErrors()
    {
        using var context = CreateContext();
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
    public void InvalidCredentials_ShowGenericError()
    {
        using var context = CreateContext();
        var component = context.Render<LoginComponent>();

        component.Find("#email").Change("candidate@example.test");
        component.Find("#password").Change("secret");
        component.Find("button[type='submit']").Click();

        Assert.AreEqual(
            "Email or password is incorrect.",
            component.Find(".submission-message").TextContent.Trim());
    }

    [TestMethod]
    public void ValidCredentials_CreateSessionAndNavigateToExam()
    {
        using var context = CreateContext(authenticationSucceeds: true);
        var component = context.Render<LoginComponent>();

        component.Find("#email").Change("candidate@example.test");
        component.Find("#password").Change("DemoExam!2026");
        component.Find("button[type='submit']").Click();

        var session = context.Services.GetRequiredService<CandidateSessionState>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.IsTrue(session.IsAuthenticated);
        StringAssert.EndsWith(navigation.Uri, "/exam");
    }

    private static BunitContext CreateContext(
        bool authenticationSucceeds = false)
    {
        var context = new BunitContext();
        context.Services.AddSingleton<CandidateSessionState>();
        context.Services.AddSingleton<IAuthenticationService>(
            new StubAuthenticationService(authenticationSucceeds));
        return context;
    }

    private sealed class StubAuthenticationService(bool succeeds)
        : IAuthenticationService
    {
        public Task<AuthenticationResult> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var result = succeeds
                ? AuthenticationResult.Success(new CandidateIdentity(
                    Guid.NewGuid(),
                    "Demo",
                    null,
                    "Candidate",
                    null,
                    email,
                    new DateOnly(2000, 1, 1)))
                : AuthenticationResult.Failed;

            return Task.FromResult(result);
        }
    }
}
