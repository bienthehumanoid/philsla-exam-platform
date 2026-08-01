using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Core.Examinations;
using ExamSessionComponent =
    PhilSLA.ExamPlatform.Candidate.Components.Pages.ExamSession;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamSessionTests
{
    private static readonly Guid CandidateId =
        Guid.Parse("238fe1c4-ec9d-4a5e-bc72-67f944659786");

    [TestMethod]
    public void ExamCannotBeOpenedBeforeAttemptStarts()
    {
        using var context = CreateContext(startExam: false);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam-session");

        context.Render<ExamSessionComponent>();

        StringAssert.EndsWith(navigation.Uri, "/exam-authorization");
    }

    [TestMethod]
    public void StartedExam_ShowsWorkspaceAndFullBlockDuration()
    {
        using var context = CreateContext(startExam: true);

        var component = context.Render<ExamSessionComponent>();

        Assert.AreEqual(
            "01:00:00",
            component.Find("[data-testid='exam-timer']").TextContent);
        StringAssert.Contains(component.Markup, "Mathematics");
        Assert.HasCount(
            3,
            component.FindAll(".palette-grid button"));
    }

    [TestMethod]
    public void SelectingAnswer_DurablyUpdatesWorkspaceState()
    {
        using var context = CreateContext(startExam: true);
        var service = context.Services.GetRequiredService<ExamSessionService>();
        var component = context.Render<ExamSessionComponent>();

        component.Find("[data-testid='answer-A']").Change(true);

        var snapshot = service.GetCurrent();
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(
            snapshot.Attempt.Answers.ContainsKey(
                snapshot.CurrentQuestion.Id));
        StringAssert.Contains(component.Markup, "Answer saved locally.");
    }

    [TestMethod]
    public void FinishingBlock_RequiresConfirmationAndStopsTimer()
    {
        using var context = CreateContext(startExam: true);
        var component = context.Render<ExamSessionComponent>();

        component.Find("[data-testid='finish-block']").Click();

        Assert.HasCount(1, component.FindAll("[role='dialog']"));

        component.Find("[data-testid='confirm-finish-block']").Click();

        StringAssert.Contains(component.Markup, "Mathematics submitted");
        Assert.HasCount(
            1,
            component.FindAll("[data-testid='start-next-block']"));
    }

    private static BunitContext CreateContext(bool startExam)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var session = new CandidateSessionState();
        session.SignIn(new CandidateIdentity(
            CandidateId,
            "Juan Carlos",
            null,
            "Villanueva",
            null,
            "candidate@example.test",
            new DateOnly(2000, 1, 1)));

        var service = new ExamSessionService(
            new TestExamDefinitionProvider(),
            new InMemoryExamAttemptStore(),
            new TestTimeProvider(
                new DateTimeOffset(
                    2026,
                    8,
                    15,
                    8,
                    0,
                    0,
                    TimeSpan.Zero)));
        if (startExam)
        {
            service.StartAsync(CandidateId).GetAwaiter().GetResult();
        }

        context.Services.AddSingleton(session);
        context.Services.AddSingleton(service);
        return context;
    }
}
