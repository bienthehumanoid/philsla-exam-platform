using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using LoginComponent = PhilSLA.ExamPlatform.Proctor.Components.Pages.Login;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class LoginTests
{
    [TestMethod]
    public void InvalidCredentials_ShowGenericError()
    {
        using var context = CreateContext();
        var component = context.Render<LoginComponent>();

        component.Find("#email").Change("proctor@example.test");
        component.Find("#password").Change("incorrect");
        component.Find("button[type='submit']").Click();

        Assert.AreEqual(
            "Email or password is incorrect.",
            component.Find(".submission-message").TextContent.Trim());
    }

    [TestMethod]
    public void ValidCredentials_CreateSessionAndNavigateToHome()
    {
        using var context = CreateContext(authenticationSucceeds: true);
        var component = context.Render<LoginComponent>();

        component.Find("#email").Change("proctor@example.test");
        component.Find("#password").Change("DemoProctor!2026");
        component.Find("button[type='submit']").Click();

        var session = context.Services.GetRequiredService<ProctorSessionState>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.IsTrue(session.IsAuthenticated);
        StringAssert.EndsWith(navigation.Uri, "/home");
    }

    private static BunitContext CreateContext(
        bool authenticationSucceeds = false)
    {
        var context = new BunitContext();
        context.Services.AddSingleton<ProctorSessionState>();
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
                ? AuthenticationResult.Success(new ProctorIdentity(
                    Guid.NewGuid(),
                    "Demo",
                    "Proctor",
                    email,
                    "PROCTOR"))
                : AuthenticationResult.Failed;

            return Task.FromResult(result);
        }
    }
}
