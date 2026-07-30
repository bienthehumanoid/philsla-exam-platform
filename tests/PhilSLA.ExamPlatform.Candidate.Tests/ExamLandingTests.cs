using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using ExamLandingComponent =
    PhilSLA.ExamPlatform.Candidate.Components.Pages.ExamLanding;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamLandingTests
{
    [TestMethod]
    public void UnauthenticatedCandidate_IsRedirectedToLogin()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<CandidateSessionState>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        context.Render<ExamLandingComponent>();

        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }

    [TestMethod]
    public void AuthenticatedCandidate_CanLogOut()
    {
        using var context = new BunitContext();
        var session = new CandidateSessionState();
        session.SignIn(new CandidateIdentity(
            Guid.NewGuid(),
            "Demo",
            null,
            "Candidate",
            null,
            "candidate@example.test",
            new DateOnly(2000, 1, 1)));
        context.Services.AddSingleton(session);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        var component = context.Render<ExamLandingComponent>();
        component.Find("[data-testid='logout']").Click();

        Assert.IsFalse(session.IsAuthenticated);
        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }
}
