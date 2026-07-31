using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Candidate.Authentication;
using PhilSLA.ExamPlatform.Candidate.Examination;
using PhilSLA.ExamPlatform.Candidate.Readiness;
using ExamLandingComponent =
    PhilSLA.ExamPlatform.Candidate.Components.Pages.ExamLanding;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class ExamLandingTests
{
    [TestMethod]
    public void UnauthenticatedCandidate_IsRedirectedToLogin()
    {
        using var context = CreateContext(authenticated: false);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        context.Render<ExamLandingComponent>();

        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }

    [TestMethod]
    public void PassingDiagnostics_RemainVisibleForManualTesting()
    {
        var authorization = new ImmediateAuthorizationService();
        using var context = CreateContext(authorization: authorization);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        context.Render<ExamLandingComponent>();

        Assert.AreEqual(0, authorization.RequestCount);
        StringAssert.EndsWith(navigation.Uri, "/exam");
    }

    [TestMethod]
    public void PassingDiagnostics_EnableProceedAndNavigateToSetup()
    {
        using var context = CreateContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");
        var component = context.Render<ExamLandingComponent>();
        var proceed = component.Find("[data-testid='proceed-to-setup']");

        Assert.IsFalse(proceed.HasAttribute("disabled"));

        proceed.Click();

        StringAssert.EndsWith(navigation.Uri, "/exam-instructions");
    }

    [TestMethod]
    public void FailedDiagnostics_BlockAuthorizationAndExplainFailure()
    {
        var authorization = new ImmediateAuthorizationService();
        var report = ReadyReport() with
        {
            Camera = new ReadinessCheck(
                ReadinessStatus.Failed,
                "Camera unavailable or in use.")
        };
        using var context = CreateContext(
            reports: [report],
            authorization: authorization);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        var component = context.Render<ExamLandingComponent>();

        Assert.AreEqual(0, authorization.RequestCount);
        StringAssert.EndsWith(navigation.Uri, "/exam");
        StringAssert.Contains(
            component.Markup,
            "Camera unavailable or in use.");
        Assert.HasCount(
            1,
            component.FindAll("[data-testid='run-diagnostics']"));
        Assert.IsTrue(
            component
                .Find("[data-testid='proceed-to-setup']")
                .HasAttribute("disabled"));
    }

    [TestMethod]
    public void RerunDiagnostics_CanRecoverWithoutLeavingPage()
    {
        var failed = ReadyReport() with
        {
            Microphone = new ReadinessCheck(
                ReadinessStatus.Failed,
                "Microphone unavailable or in use.")
        };
        var authorization = new ImmediateAuthorizationService();
        using var context = CreateContext(
            reports: [failed, ReadyReport()],
            authorization: authorization);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");
        var component = context.Render<ExamLandingComponent>();

        component.Find("[data-testid='run-diagnostics']").Click();

        Assert.AreEqual(0, authorization.RequestCount);
        StringAssert.EndsWith(navigation.Uri, "/exam");
    }

    [TestMethod]
    public void LandingPage_RemovesWebPortalActions()
    {
        var failed = ReadyReport() with
        {
            Network = new ReadinessCheck(
                ReadinessStatus.Failed,
                "No network connection detected.")
        };
        using var context = CreateContext(reports: [failed]);

        var component = context.Render<ExamLandingComponent>();

        Assert.DoesNotContain("Open Secure Software", component.Markup);
        Assert.DoesNotContain("Reschedule", component.Markup);
        Assert.DoesNotContain("Document Verification", component.Markup);
        StringAssert.Contains(component.Markup, "Exam readiness");
    }

    [TestMethod]
    public void AuthenticatedCandidate_CanLogOut()
    {
        var report = ReadyReport() with
        {
            Network = new ReadinessCheck(
                ReadinessStatus.Failed,
                "No network connection detected.")
        };
        using var context = CreateContext(reports: [report]);
        var session = context.Services.GetRequiredService<CandidateSessionState>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/exam");

        var component = context.Render<ExamLandingComponent>();
        component.Find("[data-testid='logout']").Click();

        Assert.IsFalse(session.IsAuthenticated);
        Assert.AreEqual(navigation.BaseUri, navigation.Uri);
    }

    private static BunitContext CreateContext(
        bool authenticated = true,
        IEnumerable<DeviceReadinessReport>? reports = null,
        IExamAuthorizationService? authorization = null)
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
        context.Services.AddSingleton<IDeviceReadinessService>(
            new StubReadinessService(reports ?? [ReadyReport()]));
        context.Services.AddSingleton(
            authorization ?? new ImmediateAuthorizationService());
        context.Services.AddSingleton<IExamAssignmentProvider>(
            new StubExamAssignmentProvider());
        return context;
    }

    private static DeviceReadinessReport ReadyReport()
    {
        return new DeviceReadinessReport(
            new ReadinessCheck(ReadinessStatus.Ready, "Camera ready."),
            new ReadinessCheck(ReadinessStatus.Ready, "Microphone ready."),
            new ReadinessCheck(ReadinessStatus.Ready, "Network connected."));
    }

    private sealed class StubReadinessService(
        IEnumerable<DeviceReadinessReport> reports)
        : IDeviceReadinessService
    {
        private readonly Queue<DeviceReadinessReport> _reports = new(reports);

        public Task<DeviceReadinessReport> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_reports.Dequeue());
        }
    }

    private sealed class ImmediateAuthorizationService
        : IExamAuthorizationService
    {
        public int RequestCount { get; private set; }

        public Task WaitForAuthorizationAsync(
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
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
                "Ms. Ramos"));
        }
    }
}
