using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using PhilSLA.ExamPlatform.Core.Attendance;
using PhilSLA.ExamPlatform.Proctor.Authentication;
using PhilSLA.ExamPlatform.Proctor.Tests.Attendance;
using AttendanceComponent = PhilSLA.ExamPlatform.Proctor.Components.Pages.Attendance;
using NavMenuComponent = PhilSLA.ExamPlatform.Proctor.Components.Layout.NavMenu;

namespace PhilSLA.ExamPlatform.Proctor.Tests;

[TestClass]
public sealed class AttendancePageTests
{
    private static readonly Guid SessionId = AttendanceTestData.CreateDefinition().Id;

    [TestMethod]
    public void UnauthenticatedVisitor_IsRedirectedToLogin()
    {
        using var context = CreateAttendanceContext(AttendanceTestData.StartsAtUtc);

        context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.AreEqual("http://localhost/", navigation.Uri);
    }

    [TestMethod]
    public void WrongProctorSession_IsRejected()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc,
            authenticated: true,
            proctorId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(
                "This attendance session is not assigned to your account.",
                component.Find(".page-error").TextContent.Trim());
            Assert.HasCount(0, component.FindAll("[data-student-id]"));
        });
    }

    [TestMethod]
    public void ManualCheckIn_RequiresIdentityConfirmationAndReason()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("[data-student-id]").Click();
        component.Find("#manual-reason").Change("Printed permit was unavailable.");
        Assert.IsTrue(component.Find(".confirm-check-in").HasAttribute("disabled"));
        component.Find("#identity-confirmed").Change(true);
        component.Find(".confirm-check-in").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Present", component.Find(".student-status").TextContent.Trim());
            Assert.AreEqual("Checked in — Present", component.Find(".check-in-result").TextContent.Trim());
            Assert.AreEqual("Manual", component.Find(".check-in-method").TextContent.Trim());
        });
    }

    [TestMethod]
    public void BlankReason_DisablesConfirmationAndShowsAdjacentError()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("[data-student-id]").Click();
        component.Find("#identity-confirmed").Change(true);
        component.Find("#manual-reason").Change("   ");

        Assert.IsTrue(component.Find(".confirm-check-in").HasAttribute("disabled"));
        Assert.AreEqual(
            "Enter a reason for manual check-in.",
            component.Find("#manual-reason-error").TextContent.Trim());
    }

    [TestMethod]
    public void CheckInDuringGracePeriod_RendersLateResultFromService()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(5),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        CompleteManualCheckIn(component);

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Late", component.Find(".student-status").TextContent.Trim());
            Assert.AreEqual("Checked in — Late", component.Find(".check-in-result").TextContent.Trim());
        });
    }

    [TestMethod]
    [DataRow(-31, "Check-in has not opened")]
    [DataRow(16, "Check-in closed")]
    public void CheckInOutsideWindow_ShowsExactServiceResult(int offsetMinutes, string expected)
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(offsetMinutes),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        CompleteManualCheckIn(component);

        component.WaitForAssertion(() =>
            Assert.AreEqual(expected, component.Find(".check-in-result").TextContent.Trim()));
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
    }

    [TestMethod]
    public async Task DuplicateCheckIn_ShowsAlreadyCheckedIn()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new InMemoryAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Initial identity verification.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        CompleteManualCheckIn(component);

        component.WaitForAssertion(() =>
            Assert.AreEqual("Already checked in", component.Find(".check-in-result").TextContent.Trim()));
    }

    [TestMethod]
    public void ServiceError_RemainsVisibleWithoutLosingRoster()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true,
            store: new FailingSaveAttendanceStore());
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        CompleteManualCheckIn(component);

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(
                "Manual check-in could not be saved. Try again.",
                component.Find(".check-in-error").TextContent.Trim());
            Assert.HasCount(1, component.FindAll("[data-student-id]"));
            Assert.AreEqual("Unmarked", component.Find(".student-status").TextContent.Trim());
        });
    }

    [TestMethod]
    public void Page_ShowsSessionTotalsScannerFallbackAndSearchableRoster()
    {
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "photos/ben.jpg")
        };
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true,
            definition: AttendanceTestData.CreateDefinition(students));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Civil Service Exam", component.Find("h1").TextContent.Trim());
            StringAssert.Contains(component.Find(".session-details").TextContent, "Room 101");
            StringAssert.Contains(component.Find(".session-details").TextContent, "Aug 6, 2026 · 04:00 PM – 06:00 PM");
            CollectionAssert.AreEqual(
                new[] { "03:30 PM", "04:15 PM" },
                component.FindAll(".timing-summary dd").Select(value => value.TextContent.Trim()).ToArray());
            Assert.AreEqual(
                "Mobile scanner not connected — use manual check-in",
                component.Find(".scanner-status").TextContent.Trim());
            Assert.HasCount(5, component.FindAll(".attendance-total"));
            Assert.HasCount(2, component.FindAll("[data-student-id]"));
            Assert.HasCount(2, component.FindAll(".reference-photo"));
        });

        component.Find("#student-search").Input("Ben");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        StringAssert.Contains(component.Find("[data-student-id]").TextContent, "Ben Santos");
        component.Find("#student-search").Input("2026-0001");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        StringAssert.Contains(component.Find("[data-student-id]").TextContent, "Ana Reyes");
    }

    [TestMethod]
    public void Dialog_ProvidesAccessibleFocusAndEscapeCancellation()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("[data-student-id]").Click();

        var dialog = component.Find("[role='dialog']");
        Assert.AreEqual("true", dialog.GetAttribute("aria-modal"));
        Assert.IsTrue(component.Find("#manual-reason").HasAttribute("autofocus"));
        dialog.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.HasCount(0, component.FindAll("[role='dialog']"));
    }

    [TestMethod]
    public void AttendanceNavigation_ReturnsToAssignedSessionsWithAccessibleExplanation()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc,
            authenticated: true);

        var component = context.Render<NavMenuComponent>();
        var link = component.Find(".attendance-navigation");

        Assert.AreEqual("home", link.GetAttribute("href"));
        Assert.AreEqual(
            "Student Attendance — open attendance from an assigned session card",
            link.GetAttribute("aria-label"));
    }

    private static void CompleteManualCheckIn(IRenderedComponent<AttendanceComponent> component)
    {
        component.WaitForElement("[data-student-id]").Click();
        component.Find("#manual-reason").Change("Identity confirmed against school ID.");
        component.Find("#identity-confirmed").Change(true);
        component.Find(".confirm-check-in").Click();
    }

    private static BunitContext CreateAttendanceContext(
        DateTimeOffset utcNow,
        bool authenticated = false,
        Guid? proctorId = null,
        AttendanceSessionDefinition? definition = null,
        IAttendanceSessionProvider? provider = null,
        IAttendanceStore? store = null,
        TimeProvider? timeProvider = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var session = new ProctorSessionState();
        if (authenticated)
        {
            session.SignIn(new ProctorIdentity(
                proctorId ?? AttendanceTestData.ProctorId,
                "Santiago",
                "Reyes",
                "proctor@example.test",
                "PROCTOR"));
        }

        context.Services.AddSingleton(session);
        context.Services.AddSingleton(
            provider ?? new InMemoryAttendanceSessionProvider(
                [definition ?? AttendanceTestData.CreateDefinition()]));
        context.Services.AddSingleton(store ?? new InMemoryAttendanceStore());
        context.Services.AddSingleton(timeProvider ?? new TestTimeProvider(utcNow));
        context.Services.AddSingleton<AttendanceService>();
        return context;
    }

    private sealed class FailingSaveAttendanceStore : IAttendanceStore
    {
        private AttendanceSessionRecord? _record;

        public Task<AttendanceSessionRecord?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_record);

        public Task<AttendanceSessionRecord> CreateAsync(
            AttendanceSessionDefinition definition,
            CancellationToken cancellationToken = default)
        {
            _record = new AttendanceSessionRecord(
                definition.Id,
                definition.Students.Select(student => new AttendanceEntry(
                    student.Id,
                    AttendanceStatus.Unmarked,
                    null,
                    null,
                    null,
                    null,
                    null)).ToArray(),
                [],
                null,
                0);
            return Task.FromResult(_record);
        }

        public Task<AttendanceSessionRecord> SaveAsync(
            AttendanceSessionRecord record,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic persistence failure.");
    }
}
