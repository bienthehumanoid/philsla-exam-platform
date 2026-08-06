using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PhilSLA.ExamPlatform.Core.Attendance;
using PhilSLA.ExamPlatform.Proctor.Attendance;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using PhilSLA.ExamPlatform.Proctor.Tests.Attendance;
using HomeComponent = PhilSLA.ExamPlatform.Proctor.Components.Pages.Home;
using NavMenuComponent = PhilSLA.ExamPlatform.Proctor.Components.Layout.NavMenu;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class DashboardTests
{
    [TestMethod]
    public void UnauthenticatedVisitor_IsRedirectedToLogin()
    {
        using var context = CreateContext();

        context.Render<HomeComponent>();

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.AreEqual("http://localhost/", navigation.Uri);
    }

    [TestMethod]
    public void AuthenticatedProctor_SeesStoredAttendanceTotalsAndEnabledLink()
    {
        using var context = CreateContext(authenticated: true);

        var component = context.Render<HomeComponent>();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(1, component.FindAll(".session-card"));
            Assert.AreEqual("0", component.Find(".attendance-summary dt").TextContent);
            Assert.IsFalse(component.Find(".attendance-link").HasAttribute("aria-disabled"));
            Assert.AreEqual(
                $"attendance/{AttendanceTestData.CreateDefinition().Id}",
                component.Find(".attendance-link").GetAttribute("href"));
        });
    }

    [TestMethod]
    public void SignOut_ClearsSessionAndReturnsToLogin()
    {
        using var context = CreateContext(authenticated: true);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/home");
        var component = context.Render<NavMenuComponent>();

        component.Find(".sign-out-button").Click();

        var session = context.Services.GetRequiredService<ProctorSessionState>();
        Assert.IsFalse(session.IsAuthenticated);
        Assert.AreEqual("http://localhost/", navigation.Uri);
    }

    [TestMethod]
    public async Task SeededProvider_ProjectsCallerAndPreservesFixtureIds()
    {
        var provider = new SeededAttendanceSessionProvider();
        var firstProctorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondProctorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var firstAssignment = await provider.GetAssignedSessionsAsync(firstProctorId);
        var secondAssignment = await provider.GetAssignedSessionsAsync(secondProctorId);
        var selected = await provider.GetSessionAsync(firstAssignment[0].Id, secondProctorId);

        Assert.HasCount(3, firstAssignment);
        Assert.IsTrue(firstAssignment.All(session => session.AssignedProctorId == firstProctorId));
        CollectionAssert.AreEqual(
            firstAssignment.Select(session => session.Id).ToArray(),
            secondAssignment.Select(session => session.Id).ToArray());
        CollectionAssert.AreEqual(
            firstAssignment.SelectMany(session => session.Students).Select(student => student.Id).ToArray(),
            secondAssignment.SelectMany(session => session.Students).Select(student => student.Id).ToArray());
        Assert.IsTrue(firstAssignment.All(session =>
            session.Policy.CheckInOpensBeforeStart == TimeSpan.FromMinutes(30) &&
            session.Policy.LateGracePeriod == TimeSpan.FromMinutes(15)));
        Assert.IsTrue(firstAssignment
            .SelectMany(session => session.Students)
            .All(student => student.ReferencePhotoPath.StartsWith("/images/candidates/candidate-", StringComparison.Ordinal)));
        Assert.AreEqual(secondProctorId, selected?.AssignedProctorId);
    }

    [TestMethod]
    public void AssignedSessions_AreShownAsLoadingUntilProviderCompletes()
    {
        var provider = new DeferredAttendanceSessionProvider();
        using var context = CreateContext(authenticated: true, provider);

        var component = context.Render<HomeComponent>();

        Assert.AreEqual("Loading assigned sessions...", component.Find(".sessions-loading").TextContent.Trim());

        provider.Complete([AttendanceTestData.CreateDefinition()]);
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll(".session-card")));
    }

    [TestMethod]
    public void AssignedSessions_LoadFailureCanBeRetried()
    {
        var provider = new FailOnceAttendanceSessionProvider(AttendanceTestData.CreateDefinition());
        using var context = CreateContext(authenticated: true, provider);

        var component = context.Render<HomeComponent>();

        component.WaitForAssertion(() =>
            Assert.AreEqual(
                "Assigned sessions could not be loaded.",
                component.Find(".sessions-error p").TextContent));
        component.Find(".retry-button").Click();
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll(".session-card")));
    }

    private static BunitContext CreateContext(
        bool authenticated = false,
        IAttendanceSessionProvider? provider = null)
    {
        var context = new BunitContext();
        var session = new ProctorSessionState();
        if (authenticated)
        {
            session.SignIn(new ProctorIdentity(
                AttendanceTestData.ProctorId,
                "Santiago",
                "Reyes",
                "proctor@example.test",
                "PROCTOR"));
        }

        context.Services.AddSingleton(session);
        context.Services.AddSingleton(
            provider ?? new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]));
        context.Services.AddSingleton<IAttendanceStore, InMemoryAttendanceStore>();
        context.Services.AddSingleton<TimeProvider>(
            new TestTimeProvider(AttendanceTestData.StartsAtUtc));
        context.Services.AddSingleton<AttendanceService>();
        return context;
    }

    private sealed class DeferredAttendanceSessionProvider : IAttendanceSessionProvider
    {
        private readonly TaskCompletionSource<IReadOnlyList<AttendanceSessionDefinition>> _sessions =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<AttendanceSessionDefinition>> GetAssignedSessionsAsync(
            Guid proctorId,
            CancellationToken cancellationToken = default) =>
            _sessions.Task.WaitAsync(cancellationToken);

        public Task<AttendanceSessionDefinition?> GetSessionAsync(
            Guid sessionId,
            Guid proctorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AttendanceSessionDefinition?>(null);

        public void Complete(IReadOnlyList<AttendanceSessionDefinition> sessions) =>
            _sessions.SetResult(sessions);
    }

    private sealed class FailOnceAttendanceSessionProvider(AttendanceSessionDefinition definition)
        : IAttendanceSessionProvider
    {
        private int _calls;

        public Task<IReadOnlyList<AttendanceSessionDefinition>> GetAssignedSessionsAsync(
            Guid proctorId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _calls) == 1)
            {
                throw new InvalidOperationException("Synthetic provider failure.");
            }

            IReadOnlyList<AttendanceSessionDefinition> sessions = [definition];
            return Task.FromResult(sessions);
        }

        public Task<AttendanceSessionDefinition?> GetSessionAsync(
            Guid sessionId,
            Guid proctorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AttendanceSessionDefinition?>(definition.Id == sessionId ? definition : null);
    }
}
