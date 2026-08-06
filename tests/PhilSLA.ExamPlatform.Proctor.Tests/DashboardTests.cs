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
    public async Task AuthenticatedProctor_SeesStoredAttendanceTotalsAndEnabledLinks()
    {
        var provider = new SeededAttendanceSessionProvider();
        var store = new InMemoryAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc);
        var service = new AttendanceService(provider, store, timeProvider);
        var sessions = await provider.GetAssignedSessionsAsync(AttendanceTestData.ProctorId);
        var first = sessions[0];

        timeProvider.SetUtcNow(first.StartsAtUtc.AddMinutes(-1));
        await service.CheckInAsync(
            first.Id,
            first.Students[0].Id,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Seed present total.");
        timeProvider.SetUtcNow(first.StartsAtUtc);
        await service.CheckInAsync(
            first.Id,
            first.Students[1].Id,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Seed late total.");
        timeProvider.SetUtcNow(first.StartsAtUtc.AddMinutes(15));
        await service.ApplyCutoffAsync(first.Id, AttendanceTestData.ProctorId);
        await service.ConfirmAbsentAsync(
            first.Id,
            first.Students[2].Id,
            AttendanceTestData.ProctorId);

        using var context = CreateContext(authenticated: true, provider, store, timeProvider);

        var component = context.Render<HomeComponent>();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Exam Schedule", component.Find("h1").TextContent);
            Assert.HasCount(3, component.FindAll(".session-card"));
            var totals = component.Find(".session-card").QuerySelectorAll(".attendance-summary dt");
            Assert.AreEqual("1", totals[0].TextContent);
            Assert.AreEqual("1", totals[1].TextContent);
            Assert.AreEqual("1", totals[2].TextContent);
            Assert.HasCount(3, component.FindAll(".attendance-link"));
            Assert.IsTrue(component.FindAll(".attendance-link")
                .All(link => !link.HasAttribute("aria-disabled")));
            Assert.AreEqual(
                $"attendance/{first.Id}",
                component.Find(".attendance-link").GetAttribute("href"));
            Assert.IsTrue(component.Find(".primary-button").HasAttribute("disabled"));
            Assert.HasCount(3, component.FindAll(".session-actions button"));
            Assert.IsTrue(component.FindAll(".session-actions button")
                .All(button => button.HasAttribute("disabled")));
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
    public async Task SeededProvider_UsesStableAssignmentsAndPhilippineScheduleInstants()
    {
        var firstProvider = new SeededAttendanceSessionProvider();
        var secondProvider = new SeededAttendanceSessionProvider();
        var firstAssignment = await firstProvider.GetAssignedSessionsAsync(AttendanceTestData.ProctorId);
        var secondAssignment = await secondProvider.GetAssignedSessionsAsync(AttendanceTestData.ProctorId);

        var expectedSessionIds = new[]
        {
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            Guid.Parse("40000000-0000-0000-0000-000000000003")
        };
        var expectedStartsAtUtc = new[]
        {
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 22, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 29, 5, 0, 0, TimeSpan.Zero)
        };
        var expectedEndsAtUtc = new[]
        {
            new DateTimeOffset(2026, 6, 15, 3, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 22, 4, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 29, 8, 0, 0, TimeSpan.Zero)
        };

        Assert.HasCount(3, firstAssignment);
        Assert.IsTrue(firstAssignment.All(session =>
            session.AssignedProctorId == AttendanceTestData.ProctorId));
        CollectionAssert.AreEqual(
            expectedSessionIds,
            firstAssignment.Select(session => session.Id).ToArray());
        CollectionAssert.AreEqual(
            expectedSessionIds,
            secondAssignment.Select(session => session.Id).ToArray());
        CollectionAssert.AreEqual(
            expectedStartsAtUtc,
            firstAssignment.Select(session => session.StartsAtUtc).ToArray());
        CollectionAssert.AreEqual(
            expectedEndsAtUtc,
            firstAssignment.Select(session => session.EndsAtUtc).ToArray());
        CollectionAssert.AreEqual(
            firstAssignment.SelectMany(session => session.Students).Select(student => student.Id).ToArray(),
            secondAssignment.SelectMany(session => session.Students).Select(student => student.Id).ToArray());
        Assert.IsTrue(firstAssignment.All(session =>
            session.Policy.CheckInOpensBeforeStart == TimeSpan.FromMinutes(30) &&
            session.Policy.LateGracePeriod == TimeSpan.FromMinutes(15)));
        Assert.IsTrue(firstAssignment
            .SelectMany(session => session.Students)
            .All(student => student.ReferencePhotoPath.StartsWith("/images/candidates/candidate-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SeededProvider_RejectsEveryOtherProctorWithoutSharingAttendanceState()
    {
        var provider = new SeededAttendanceSessionProvider();
        var store = new InMemoryAttendanceStore();
        var assigned = await provider.GetAssignedSessionsAsync(AttendanceTestData.ProctorId);
        var first = assigned[0];
        var timeProvider = new TestTimeProvider(first.StartsAtUtc.AddMinutes(-1));
        var service = new AttendanceService(provider, store, timeProvider);
        var otherProctorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var checkedIn = await service.CheckInAsync(
            first.Id,
            first.Students[0].Id,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Verify assigned-proctor isolation.");

        Assert.AreEqual(1, checkedIn.PresentCount);
        Assert.HasCount(0, await provider.GetAssignedSessionsAsync(otherProctorId));
        Assert.IsNull(await provider.GetSessionAsync(first.Id, otherProctorId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LoadAsync(first.Id, otherProctorId));
        Assert.AreEqual(
            1,
            (await service.LoadAsync(first.Id, AttendanceTestData.ProctorId)).PresentCount);
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
        IAttendanceSessionProvider? provider = null,
        IAttendanceStore? store = null,
        TimeProvider? timeProvider = null)
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
        context.Services.AddSingleton(store ?? new InMemoryAttendanceStore());
        context.Services.AddSingleton(
            timeProvider ?? new TestTimeProvider(AttendanceTestData.StartsAtUtc));
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
