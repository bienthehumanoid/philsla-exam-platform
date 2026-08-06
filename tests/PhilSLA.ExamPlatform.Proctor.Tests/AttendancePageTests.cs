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

        component.WaitForElement(".check-in-attendance").Click();
        component.Find("#manual-reason").Change("Printed permit was unavailable.");
        Assert.IsTrue(component.Find(".confirm-check-in").HasAttribute("disabled"));
        component.Find("#identity-confirmed").Change(true);
        component.Find(".confirm-check-in").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Present", component.Find(".student-status").TextContent.Trim());
            Assert.AreEqual("Checked in — Present", component.Find(".check-in-result").TextContent.Trim());
            Assert.IsTrue(component.Find(".check-in-result").ClassList.Contains("outcome-success"));
            Assert.AreEqual("Manual", component.Find(".check-in-method").TextContent.Trim());
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.close"));
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

        component.WaitForElement(".check-in-attendance").Click();
        component.Find("#identity-confirmed").Change(true);
        component.Find("#manual-reason").Change("   ");

        Assert.IsTrue(component.Find(".confirm-check-in").HasAttribute("disabled"));
        Assert.IsTrue(component.Find("#manual-reason").HasAttribute("required"));
        Assert.AreEqual("true", component.Find("#manual-reason").GetAttribute("aria-required"));
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
            Assert.IsTrue(component.Find(".check-in-result").ClassList.Contains("outcome-warning"));
        });
    }

    [TestMethod]
    [DataRow(-31, 1, 0)]
    [DataRow(16, 0, 1)]
    public void CheckInOutsideWindow_DoesNotOfferInvalidCheckIn(
        int offsetMinutes,
        int expectedCheckInButtons,
        int expectedConfirmAbsentButtons)
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(offsetMinutes),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(expectedCheckInButtons, component.FindAll(".check-in-attendance"));
            Assert.HasCount(expectedConfirmAbsentButtons, component.FindAll(".confirm-absent"));
        });
        if (expectedCheckInButtons == 1)
        {
            Assert.IsTrue(component.Find(".check-in-attendance").HasAttribute("disabled"));
        }
    }

    [TestMethod]
    public async Task DuplicateCheckIn_ShowsAlreadyCheckedIn()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new InMemoryAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("[data-student-id]");
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Concurrent identity verification.");

        CompleteManualCheckIn(component);

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Already checked in", component.Find(".check-in-result").TextContent.Trim());
            Assert.IsTrue(component.Find(".check-in-result").ClassList.Contains("outcome-info"));
        });
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
    [DataRow(-31, "Check-in not open")]
    [DataRow(-10, "Attendance in progress")]
    [DataRow(16, "Absence review required")]
    public void Page_ShowsTruthfulSessionState(int offsetMinutes, string expected)
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(offsetMinutes),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
            StringAssert.Contains(
                component.Find(".attendance-state-banner").TextContent,
                expected));
    }

    [TestMethod]
    public void Page_ShowsSessionTotalsScannerFallbackAndSearchableRoster()
    {
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "A02", "photos/ben.jpg")
        };
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true,
            definition: AttendanceTestData.CreateDefinition(students));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Candidate Attendance", component.Find("h1").TextContent.Trim());
            StringAssert.Contains(component.Find(".session-heading").TextContent, "Civil Service Exam");
            StringAssert.Contains(component.Find(".session-details").TextContent, "Room 101");
            StringAssert.Contains(component.Find(".session-details").TextContent, "Aug 6, 2026 · 04:00 PM – 06:00 PM");
            StringAssert.Contains(component.Find(".session-facts").TextContent, "CSE-2026-A");
            StringAssert.Contains(component.Find(".session-facts").TextContent, "03:30 PM");
            StringAssert.Contains(component.Find(".session-facts").TextContent, "04:15 PM");
            Assert.IsTrue(component.Find(".connect-scanner").HasAttribute("disabled"));
            StringAssert.Contains(component.Find(".scanner-availability").TextContent, "Not available yet");
            Assert.HasCount(6, component.FindAll(".attendance-summary-card"));
            Assert.HasCount(2, component.FindAll("[data-student-id]"));
            Assert.HasCount(0, component.FindAll(".reference-photo"));
            Assert.HasCount(1, component.FindAll("table.roster-table"));
            Assert.AreEqual("A01", component.Find(".seat-cell").TextContent.Trim());
            Assert.HasCount(2, component.FindAll(".check-in-attendance"));
            Assert.HasCount(0, component.FindAll(".present-attendance"));
            var firstAction = component.Find(".check-in-attendance");
            Assert.AreEqual(
                "Check in Ana Reyes, student number 2026-0001, seat A01",
                firstAction.GetAttribute("aria-label"));
        });

        component.Find("#student-search").Input("Ben");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        StringAssert.Contains(component.Find("[data-student-id]").TextContent, "Ben Santos");
        component.Find("#student-search").Input("2026-0001");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        StringAssert.Contains(component.Find("[data-student-id]").TextContent, "Ana Reyes");
    }

    [TestMethod]
    public async Task StatusFilter_CombinesWithSearch()
    {
        var secondStudentId = Guid.NewGuid();
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(secondStudentId, "2026-0002", "Ben Santos", "A02", "photos/ben.jpg")
        };
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition(students)]);
        var store = new InMemoryAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed against school ID.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("#attendance-status-filter")
            .Change(nameof(AttendanceStatus.Present));
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        StringAssert.Contains(component.Find("[data-student-id]").TextContent, "Ana Reyes");

        component.Find("#student-search").Input("No match");
        Assert.HasCount(0, component.FindAll("[data-student-id]"));
        component.Find("#student-search").Input("Ana");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
    }

    [TestMethod]
    [DataRow(-10, "Unmarked", 1, 0)]
    [DataRow(16, "Pending", 0, 1)]
    public void UnresolvedRows_DoNotOfferAttendanceCorrection(
        int offsetMinutes,
        string expectedStatus,
        int expectedCheckInButtons,
        int expectedConfirmAbsentButtons)
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(offsetMinutes),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(expectedStatus, component.Find(".student-status").TextContent.Trim());
            Assert.HasCount(0, component.FindAll(".correct-attendance"));
            Assert.HasCount(expectedCheckInButtons, component.FindAll(".check-in-attendance"));
            Assert.HasCount(expectedConfirmAbsentButtons, component.FindAll(".confirm-absent"));
        });
    }

    [TestMethod]
    public void Dialog_ProvidesAccessibleFocusTrapAndEscapeRestoration()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".check-in-attendance").Click();

        var dialog = component.Find("[role='dialog']");
        Assert.AreEqual("true", dialog.GetAttribute("aria-modal"));
        Assert.IsTrue(component.Find("#manual-reason").HasAttribute("autofocus"));
        component.WaitForAssertion(() =>
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.open")));
        dialog.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        component.WaitForAssertion(() =>
        {
            Assert.HasCount(0, component.FindAll("[role='dialog']"));
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.close"));
        });
    }

    [TestMethod]
    public void Dialog_CancelRestoresTriggerFocus()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".check-in-attendance").Click();
        component.Find(".cancel-check-in").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(0, component.FindAll("[role='dialog']"));
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.close"));
        });
    }

    [TestMethod]
    public void TimerRefresh_RendersServiceOwnedCutoffStatus()
    {
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(14));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
            Assert.AreEqual("Unmarked", component.Find(".student-status").TextContent.Trim()));
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        component.WaitForAssertion(() =>
            Assert.AreEqual("Pending", component.Find(".student-status").TextContent.Trim()));
    }

    [TestMethod]
    public void EndSession_IsBlockedUntilPendingAbsencesAreConfirmed()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        var endSession = component.WaitForElement(".end-session");

        Assert.IsTrue(endSession.HasAttribute("disabled"));
        Assert.HasCount(0, component.FindAll(".finalization-dialog"));
    }

    [TestMethod]
    public void PendingAbsenceReview_ConfirmsOneStudentAsAbsent()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        Assert.AreEqual("1", component.WaitForElement(".pending-count").TextContent.Trim());
        component.Find(".confirm-absent").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Absent", component.Find(".student-status").TextContent.Trim());
            Assert.HasCount(0, component.FindAll(".absence-review"));
        });
    }

    [TestMethod]
    public void PendingAbsenceReview_ConfirmsAllDisplayedStudentsAsAbsent()
    {
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "A02", "photos/ben.jpg")
        };
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true,
            definition: AttendanceTestData.CreateDefinition(students));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".confirm-all-absent").Click();
        Assert.HasCount(1, component.FindAll(".bulk-absence-dialog"));
        StringAssert.Contains(
            component.Find(".bulk-absence-dialog").TextContent,
            "2 displayed students");
        Assert.HasCount(2, component.FindAll(".student-status.status-pendingabsence"));

        component.Find(".confirm-bulk-absence").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(2, component.FindAll(".student-status.status-absent"));
            Assert.HasCount(0, component.FindAll(".confirm-absent"));
        });
    }

    [TestMethod]
    public void BulkAbsenceConfirmation_StopsOnFailureAndKeepsDurableChangesVisible()
    {
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "A02", "photos/ben.jpg")
        };
        var store = new FailOnSaveCallAttendanceStore(failOnSaveCall: 3);
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true,
            definition: AttendanceTestData.CreateDefinition(students),
            store: store);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".confirm-all-absent").Click();
        component.Find(".confirm-bulk-absence").Click();

        component.WaitForAssertion(() =>
        {
            CollectionAssert.AreEqual(
                new[] { "Absent", "Pending" },
                component.FindAll(".student-status")
                    .Select(status => status.TextContent.Trim())
                    .ToArray());
            StringAssert.Contains(
                component.Find(".operation-error").TextContent,
                "Bulk confirmation stopped after 1 durable confirmation. Try again.");
            Assert.IsFalse(component.Find(".operation-error").TextContent.Contains(
                "Synthetic bulk persistence failure",
                StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void BulkAbsenceConfirmation_AffectsOnlyDisplayedPendingStudents()
    {
        var students = new[]
        {
            new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "A02", "photos/ben.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0003", "Cara Lim", "A03", "photos/cara.jpg")
        };
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true,
            definition: AttendanceTestData.CreateDefinition(students));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement("#student-search").Input("Ana");
        component.Find("#attendance-status-filter").Change(nameof(AttendanceStatus.PendingAbsence));
        Assert.HasCount(1, component.FindAll("[data-student-id]"));

        component.Find(".confirm-all-absent").Click();
        StringAssert.Contains(component.Find(".bulk-absence-dialog").TextContent, "1 displayed student");
        component.Find(".confirm-bulk-absence").Click();

        component.WaitForAssertion(() =>
            Assert.HasCount(0, component.FindAll(".bulk-absence-dialog")));
        component.Find("#student-search").Input(string.Empty);
        component.Find("#attendance-status-filter").Change(string.Empty);
        Assert.HasCount(1, component.FindAll(".student-status.status-absent"));
        Assert.HasCount(2, component.FindAll(".student-status.status-pendingabsence"));
    }

    [TestMethod]
    public void BulkAbsenceConfirmation_CanBeCancelledWithoutChanges()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".confirm-all-absent").Click();
        component.Find(".cancel-bulk-absence").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(0, component.FindAll(".bulk-absence-dialog"));
            Assert.HasCount(1, component.FindAll(".student-status.status-pendingabsence"));
            Assert.HasCount(0, component.FindAll(".student-status.status-absent"));
        });
    }

    [TestMethod]
    public void IndividualAbsenceInfrastructureFailure_ShowsSafeGenericCopy()
    {
        var store = new FailOnSaveCallAttendanceStore(failOnSaveCall: 2);
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true,
            store: store);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".confirm-absent").Click();

        component.WaitForAssertion(() =>
            Assert.AreEqual(
                "Absence could not be confirmed. Try again.",
                component.Find(".operation-error").TextContent.Trim()));
    }

    [TestMethod]
    public async Task Correction_RequiresReasonAndShowsNewestAuditHistory()
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
            manualReason: "Identity confirmed against school ID.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".correct-attendance").Click();
        component.Find("#correction-status").Change(nameof(AttendanceStatus.Late));
        Assert.IsTrue(component.Find(".save-correction").HasAttribute("disabled"));
        Assert.AreEqual(
            "Enter a reason for this correction.",
            component.Find("#correction-reason-error").TextContent.Trim());
        component.Find("#correction-reason").Change("Arrival time was copied incorrectly.");
        component.Find(".save-correction").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Late", component.Find(".student-status").TextContent.Trim());
            var history = component.Find("details.attendance-history");
            Assert.IsFalse(history.HasAttribute("open"));
            StringAssert.Contains(
                history.QuerySelector("summary")!.TextContent,
                "Attendance history (2)");
            Assert.HasCount(2, history.QuerySelectorAll(".audit-entry"));
            var newestAudit = history.QuerySelector(".audit-entry")!;
            StringAssert.Contains(newestAudit.TextContent, "Present");
            StringAssert.Contains(newestAudit.TextContent, "Late");
            StringAssert.Contains(newestAudit.TextContent, "Arrival time was copied incorrectly.");
            StringAssert.Contains(newestAudit.TextContent, "Santiago Reyes");
            StringAssert.Contains(newestAudit.TextContent, "03:50 PM");
        });
    }

    [TestMethod]
    public async Task CorrectionSuccess_RemainsDurableWhenFocusTeardownFails()
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
            manualReason: "Identity confirmed against school ID.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        context.JSInterop
            .SetupVoid("philslaAttendanceDialog.close")
            .SetException(new JSException("Synthetic focus teardown failure."));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".correct-attendance").Click();
        component.Find("#correction-status").Change(nameof(AttendanceStatus.Late));
        component.Find("#correction-reason").Change("Arrival time was copied incorrectly.");
        component.Find(".save-correction").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Late", component.Find(".student-status").TextContent.Trim());
            Assert.HasCount(0, component.FindAll(".correction-dialog"));
            Assert.HasCount(0, component.FindAll(".correction-error"));
            StringAssert.Contains(component.Find(".operation-result").TextContent, "Attendance corrected");
        });
    }

    [TestMethod]
    public void PostCutoffFirstAdmissionCorrection_ShowsExactServiceRejection()
    {
        using var context = CreateAttendanceContext(
            AttendanceTestData.StartsAtUtc.AddMinutes(16),
            authenticated: true);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".confirm-absent").Click();
        component.WaitForElement(".correct-attendance").Click();
        component.Find("#correction-status").Change(nameof(AttendanceStatus.Present));
        component.Find("#correction-reason").Change("Student arrived after cutoff.");
        component.Find(".save-correction").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(
                "Post-cutoff admission requires pre-cutoff check-in evidence.",
                component.Find(".correction-error").TextContent.Trim());
            Assert.HasCount(1, component.FindAll(".correction-dialog"));
        });
    }

    [TestMethod]
    public async Task CorrectionInfrastructureFailure_ShowsSafeGenericCopy()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new FailOnSaveCallAttendanceStore(failOnSaveCall: 2);
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed against school ID.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".correct-attendance").Click();
        component.Find("#correction-status").Change(nameof(AttendanceStatus.Late));
        component.Find("#correction-reason").Change("Arrival time was copied incorrectly.");
        component.Find(".save-correction").Click();

        component.WaitForAssertion(() =>
        {
            var error = component.Find(".correction-error").TextContent.Trim();
            Assert.AreEqual("The attendance correction could not be saved. Try again.", error);
            Assert.IsFalse(error.Contains("Synthetic bulk persistence failure", StringComparison.Ordinal));
            Assert.HasCount(1, component.FindAll(".correction-dialog"));
        });
    }

    [TestMethod]
    public async Task Finalization_RequiresExplicitTotalsConfirmationAndMakesPageReadOnly()
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
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".end-session").Click();
        component.WaitForAssertion(() =>
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.open")));
        CollectionAssert.AreEqual(
            new[] { "1", "0", "0" },
            component.FindAll(".finalization-total dd")
                .Select(value => value.TextContent.Trim())
                .ToArray());
        Assert.IsTrue(component.Find(".finalize-attendance").HasAttribute("disabled"));
        component.Find("#finalization-confirmed").Change(true);
        component.Find(".finalize-attendance").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual(
                "Attendance finalized — read-only",
                component.Find(".finalized-banner").TextContent.Trim());
            Assert.IsFalse(component.Find(".roster-panel").HasAttribute("aria-disabled"));
            Assert.IsFalse(component.Find(".roster-panel").HasAttribute("aria-readonly"));
            Assert.HasCount(0, component.FindAll(".check-in-attendance"));
            Assert.HasCount(0, component.FindAll(".correct-attendance"));
            Assert.IsTrue(component.Find(".end-session").HasAttribute("disabled"));
            Assert.HasCount(0, component.FindAll(".confirm-absent"));
            Assert.HasCount(1, component.FindAll("details.attendance-history"));
            Assert.IsFalse(component.Find("details.attendance-history").HasAttribute("open"));
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.closeTo" &&
                invocation.Arguments.Single()?.ToString() == "attendance-finalized-status"));
        });

        var search = component.Find("#student-search");
        Assert.IsFalse(search.HasAttribute("disabled"));
        search.Input("No matching student");
        Assert.HasCount(0, component.FindAll("[data-student-id]"));
        search.Input("Ana");
        Assert.HasCount(1, component.FindAll("[data-student-id]"));
        Assert.IsFalse(component.Find("#attendance-status-filter").HasAttribute("disabled"));
        Assert.HasCount(0, component.FindAll(".check-in-attendance"));
        Assert.HasCount(0, component.FindAll(".correct-attendance"));
    }

    [TestMethod]
    public async Task FinalizationSuccess_RemainsReadOnlyWhenFocusMoveFails()
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
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        context.JSInterop
            .SetupVoid("philslaAttendanceDialog.closeTo", "attendance-finalized-status")
            .SetException(new JSException("Synthetic final focus failure."));
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".end-session").Click();
        component.Find("#finalization-confirmed").Change(true);
        component.Find(".finalize-attendance").Click();

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Attendance finalized — read-only", component.Find(".finalized-banner").TextContent.Trim());
            Assert.HasCount(0, component.FindAll(".finalization-dialog"));
            Assert.IsTrue(component.Find(".end-session").HasAttribute("disabled"));
            Assert.HasCount(0, component.FindAll(".check-in-attendance"));
            Assert.HasCount(0, component.FindAll(".correct-attendance"));
        });
    }

    [TestMethod]
    public async Task FinalizationRejection_WithConcurrentPendingStudent_StaysOpenAndNamesStudent()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new FinalizationRaceAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".end-session").Click();
        store.RejectFinalizationWithPendingStudent();
        component.Find("#finalization-confirmed").Change(true);
        component.Find(".finalize-attendance").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(1, component.FindAll(".finalization-dialog"));
            StringAssert.Contains(component.Find(".finalization-error").TextContent, "Confirm all pending absences");
            StringAssert.Contains(component.Find(".finalization-error").TextContent, "Ana Reyes");
            Assert.IsFalse(component.Find(".finalize-attendance").HasAttribute("disabled"));
        });
    }

    [TestMethod]
    public async Task FinalizationRejection_WhenConcurrentFinalizationWon_ClosesReadOnly()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new FinalizationRaceAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".end-session").Click();
        store.RejectFinalizationAsAlreadyFinalized(timeProvider.GetUtcNow());
        component.Find("#finalization-confirmed").Change(true);
        component.Find(".finalize-attendance").Click();

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(0, component.FindAll(".finalization-dialog"));
            Assert.AreEqual("Attendance finalized — read-only", component.Find(".finalized-banner").TextContent.Trim());
            Assert.IsTrue(component.Find(".end-session").HasAttribute("disabled"));
            Assert.HasCount(0, component.FindAll(".check-in-attendance"));
            Assert.HasCount(0, component.FindAll(".correct-attendance"));
        });
    }

    [TestMethod]
    public async Task FinalizationRejection_WhenRecoveryLoadFails_ShowsSafeGenericFailure()
    {
        var provider = new InMemoryAttendanceSessionProvider([AttendanceTestData.CreateDefinition()]);
        var store = new FinalizationRecoveryFailureAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var service = new AttendanceService(provider, store, timeProvider);
        await service.CheckInAsync(
            SessionId,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2).AddMinutes(1));
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".end-session").Click();
        store.FailFinalizationAndRecoveryLoad();
        component.Find("#finalization-confirmed").Change(true);
        component.Find(".finalize-attendance").Click();

        component.WaitForAssertion(() =>
        {
            var error = component.Find(".finalization-error").TextContent.Trim();
            Assert.AreEqual(
                "Attendance could not be finalized. Review the roster and try again.",
                error);
            Assert.IsFalse(error.Contains("Synthetic", StringComparison.Ordinal));
            Assert.HasCount(1, component.FindAll(".finalization-dialog"));
            Assert.IsFalse(component.Find(".finalize-attendance").HasAttribute("disabled"));
        });
    }

    [TestMethod]
    public async Task PreviouslyFinalizedSession_LoadsInReadOnlyState()
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
            manualReason: "Identity confirmed against school ID.");
        timeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddHours(2));
        await service.FinalizeAsync(SessionId, AttendanceTestData.ProctorId);
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);

        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForAssertion(() =>
        {
            Assert.AreEqual("Attendance finalized — read-only", component.Find(".finalized-banner").TextContent.Trim());
            Assert.HasCount(1, component.FindAll("[data-student-id]"));
            Assert.HasCount(0, component.FindAll(".check-in-attendance"));
            Assert.HasCount(0, component.FindAll(".correct-attendance"));
            Assert.HasCount(0, component.FindAll(".page-error"));
        });
    }

    [TestMethod]
    public async Task NewDialogs_UseAttendanceFocusTrapAndRestoreHelper()
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
            manualReason: "Identity confirmed against school ID.");
        using var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            provider: provider,
            store: store,
            timeProvider: timeProvider);
        var component = context.Render<AttendanceComponent>(
            parameters => parameters.Add(page => page.SessionId, SessionId));

        component.WaitForElement(".correct-attendance").Click();
        component.WaitForAssertion(() =>
            Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.open")));
        component.Find(".cancel-correction").Click();
        component.WaitForAssertion(() =>
            Assert.AreEqual(1, context.JSInterop.Invocations.Count(invocation =>
                invocation.Identifier == "philslaAttendanceDialog.close")));
    }

    [TestMethod]
    public async Task Disposal_CancelsInFlightTimerRefresh()
    {
        var store = new BlockingRefreshAttendanceStore();
        var timeProvider = new TestTimeProvider(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var context = CreateAttendanceContext(
            timeProvider.GetUtcNow(),
            authenticated: true,
            store: store,
            timeProvider: timeProvider);
        var disposed = false;
        try
        {
            var component = context.Render<AttendanceComponent>(
                parameters => parameters.Add(page => page.SessionId, SessionId));
            component.WaitForElement("[data-student-id]");
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await store.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.IsTrue(store.RefreshCancellationToken.CanBeCanceled);
            var disposeTask = Task.Run(context.Dispose);
            await store.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));
            disposed = true;
        }
        finally
        {
            store.Release();
            if (!disposed)
            {
                context.Dispose();
            }
        }
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
        component.WaitForElement(".check-in-attendance").Click();
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

    private sealed class FailOnSaveCallAttendanceStore(int failOnSaveCall) : IAttendanceStore
    {
        private readonly InMemoryAttendanceStore _inner = new();
        private int _saveCallCount;

        public Task<AttendanceSessionRecord?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            _inner.LoadAsync(sessionId, cancellationToken);

        public Task<AttendanceSessionRecord> CreateAsync(
            AttendanceSessionDefinition definition,
            CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(definition, cancellationToken);

        public Task<AttendanceSessionRecord> SaveAsync(
            AttendanceSessionRecord record,
            int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveCallCount) == failOnSaveCall)
            {
                throw new InvalidOperationException("Synthetic bulk persistence failure.");
            }

            return _inner.SaveAsync(record, expectedVersion, cancellationToken);
        }
    }

    private sealed class FinalizationRaceAttendanceStore : IAttendanceStore
    {
        private readonly InMemoryAttendanceStore _inner = new();
        private AttendanceSessionRecord? _raceRecord;

        public async Task<AttendanceSessionRecord?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (_raceRecord is not null)
            {
                return _raceRecord with
                {
                    Entries = _raceRecord.Entries.ToArray(),
                    AuditEntries = _raceRecord.AuditEntries.ToArray()
                };
            }

            return await _inner.LoadAsync(sessionId, cancellationToken);
        }

        public Task<AttendanceSessionRecord> CreateAsync(
            AttendanceSessionDefinition definition,
            CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(definition, cancellationToken);

        public Task<AttendanceSessionRecord> SaveAsync(
            AttendanceSessionRecord record,
            int expectedVersion,
            CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(record, expectedVersion, cancellationToken);

        public void RejectFinalizationWithPendingStudent()
        {
            var record = _inner.LoadAsync(SessionId).GetAwaiter().GetResult()!;
            _raceRecord = record with
            {
                Entries = record.Entries
                    .Select(entry => entry with { Status = AttendanceStatus.PendingAbsence })
                    .ToArray()
            };
        }

        public void RejectFinalizationAsAlreadyFinalized(DateTimeOffset finalizedAtUtc)
        {
            var record = _inner.LoadAsync(SessionId).GetAwaiter().GetResult()!;
            _raceRecord = record with { FinalizedAtUtc = finalizedAtUtc };
        }
    }

    private sealed class FinalizationRecoveryFailureAttendanceStore : IAttendanceStore
    {
        private readonly InMemoryAttendanceStore _inner = new();
        private bool _failFinalization;
        private bool _failRecoveryLoad;

        public Task<AttendanceSessionRecord?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (_failRecoveryLoad)
            {
                _failRecoveryLoad = false;
                throw new InvalidOperationException("Synthetic recovery load failure.");
            }

            return _inner.LoadAsync(sessionId, cancellationToken);
        }

        public Task<AttendanceSessionRecord> CreateAsync(
            AttendanceSessionDefinition definition,
            CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(definition, cancellationToken);

        public Task<AttendanceSessionRecord> SaveAsync(
            AttendanceSessionRecord record,
            int expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (_failFinalization)
            {
                _failFinalization = false;
                _failRecoveryLoad = true;
                throw new InvalidOperationException("Synthetic finalization persistence failure.");
            }

            return _inner.SaveAsync(record, expectedVersion, cancellationToken);
        }

        public void FailFinalizationAndRecoveryLoad() => _failFinalization = true;
    }

    private sealed class BlockingRefreshAttendanceStore : IAttendanceStore
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AttendanceSessionRecord? _record;
        private int _loadCount;

        public TaskCompletionSource RefreshStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken RefreshCancellationToken { get; private set; }

        public async Task<AttendanceSessionRecord?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _loadCount) < 3)
            {
                return _record;
            }

            RefreshCancellationToken = cancellationToken;
            RefreshStarted.TrySetResult();
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return _record;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
        }

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
            throw new InvalidOperationException("Save was not expected during this test.");

        public void Release() => _release.TrySetResult();
    }
}
