using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using ExamInstructionsComponent =
    PhilSLA.ExamPlatform.Candidate.Components.Pages.ExamInstructions;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamInstructionsTests
{
    [TestMethod]
    public void UnauthenticatedCandidate_IsRedirectedToLogin()
    {
        using var context = CreateContext(authenticated: false);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam-instructions");

        context.Render<ExamInstructionsComponent>();

        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }

    [TestMethod]
    public void TermsMustBeAcceptedBeforeContinuing()
    {
        using var context = CreateContext();
        var component = context.Render<ExamInstructionsComponent>();
        var continueButton =
            component.Find("[data-testid='agree-and-continue']");

        Assert.IsTrue(continueButton.HasAttribute("disabled"));

        component.Find("[data-testid='terms-agreement']").Change(true);

        continueButton = component.Find("[data-testid='agree-and-continue']");
        Assert.IsFalse(continueButton.HasAttribute("disabled"));
    }

    [TestMethod]
    public void AcceptingTerms_ShowsTheCurrentIterationBoundary()
    {
        using var context = CreateContext();
        var component = context.Render<ExamInstructionsComponent>();

        component.Find("[data-testid='terms-agreement']").Change(true);
        component.Find("[data-testid='agree-and-continue']").Click();

        StringAssert.Contains(
            component.Find("#terms-status").TextContent,
            "webcam check will be added in the next iteration");
        Assert.IsTrue(
            component
                .Find("[data-testid='agree-and-continue']")
                .HasAttribute("disabled"));
    }

    private static BunitContext CreateContext(bool authenticated = true)
    {
        var context = new BunitContext();
        var session = new CandidateSessionState();
        if (authenticated)
        {
            session.SignIn(new CandidateIdentity(
                Guid.Parse("238fe1c4-ec9d-4a5e-bc72-67f944659786"),
                "Demo",
                null,
                "Candidate",
                null,
                "candidate@example.test",
                new DateOnly(2000, 1, 1)));
        }

        context.Services.AddSingleton(session);
        return context;
    }
}
