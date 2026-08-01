using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Candidate.Examination;
using PhilSLA.ExamPlatform.Core.Examinations;
using ExamAuthorizationComponent =
    PhilSLA.ExamPlatform.Candidate.Components.Pages.ExamAuthorization;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamAuthorizationTests
{
    [TestMethod]
    public void UnauthenticatedCandidate_IsRedirectedToLogin()
    {
        using var context = CreateContext(authenticated: false);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam-authorization");

        context.Render<ExamAuthorizationComponent>();

        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }

    [TestMethod]
    public void ReleasedCandidate_CanStartTimedExam()
    {
        using var context = CreateContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var session = context.Services.GetRequiredService<CandidateSessionState>();
        var examSession = context.Services.GetRequiredService<ExamSessionService>();
        navigation.NavigateTo("/exam-authorization");

        var component = context.Render<ExamAuthorizationComponent>();
        var startButton = component.Find("[data-testid='start-exam']");

        Assert.IsFalse(startButton.HasAttribute("disabled"));

        startButton.Click();

        StringAssert.EndsWith(navigation.Uri, "/exam-session");
        var snapshot = examSession.GetCurrent();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(session.Candidate!.Id, snapshot.Attempt.CandidateId);
        Assert.IsGreaterThan(
            TimeSpan.FromMinutes(59),
            snapshot.RemainingTime);
    }

    private static BunitContext CreateContext(bool authenticated = true)
    {
        var context = new BunitContext();
        var session = new CandidateSessionState();
        if (authenticated)
        {
            session.SignIn(new CandidateIdentity(
                Guid.Parse("238fe1c4-ec9d-4a5e-bc72-67f944659786"),
                "Juan Carlos",
                null,
                "Villanueva",
                null,
                "candidate@example.test",
                new DateOnly(2000, 1, 1)));
        }

        context.Services.AddSingleton(session);
        context.Services.AddSingleton<IExamAuthorizationService>(
            new ImmediateAuthorizationService());
        context.Services.AddSingleton<IExamAssignmentProvider>(
            new StubExamAssignmentProvider());
        context.Services.AddSingleton(
            new ExamSessionService(
                new TestExamDefinitionProvider(),
                new InMemoryExamAttemptStore(),
                TimeProvider.System));
        return context;
    }

    private sealed class ImmediateAuthorizationService
        : IExamAuthorizationService
    {
        public Task WaitForAuthorizationAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubExamAssignmentProvider
        : IExamAssignmentProvider
    {
        public Task<ExamAssignment> GetAssignmentAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExamAssignment(
                "PhilSLA 2026 Global Assessment",
                new DateOnly(2026, 8, 15),
                "Ateneo de Manila University",
                "SEC Lecture Hall 1",
                "Adrias, M."));
        }
    }
}
