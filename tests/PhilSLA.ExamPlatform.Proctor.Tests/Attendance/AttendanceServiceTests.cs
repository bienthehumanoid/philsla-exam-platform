using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class AttendanceServiceTests
{
    [TestMethod]
    public async Task ManualCheckIn_BeforeStart_IsPresentAndAdmissible()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        var snapshot = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Printed permit was unavailable.");

        Assert.AreEqual(AttendanceStatus.Present, snapshot.Entries[0].Status);
        Assert.IsTrue(await fixture.Service.CanAdmitAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId));
    }

    [TestMethod]
    public async Task ManualCheckIn_AtStart_IsLate()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc);

        var snapshot = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        Assert.AreEqual(AttendanceStatus.Late, snapshot.Entries[0].Status);
        Assert.IsTrue(await fixture.Service.CanAdmitAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId));
    }

    [TestMethod]
    public async Task CheckIn_AtCutoff_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(15));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster."));
    }

    [TestMethod]
    public async Task ManualCheckIn_WithBlankReason_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "  "));
    }

    [TestMethod]
    public async Task QrCheckIn_WithBlankCredentialId_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Qr,
            AttendanceTestData.ProctorId,
            credentialId: " ",
            manualReason: null));
    }

    [TestMethod]
    public async Task CheckIn_WithUndefinedMethod_IsRejectedBeforeStateIsCreated()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            (AttendanceCheckInMethod)2,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: null));

        Assert.IsNull(await fixture.Store.LoadAsync(fixture.Definition.Id));
    }

    [TestMethod]
    public async Task CheckIn_ByWrongProctor_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            Guid.NewGuid(),
            credentialId: null,
            manualReason: "Identity confirmed from the roster."));
    }

    [TestMethod]
    public async Task CheckIn_ForUnassignedStudent_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            Guid.NewGuid(),
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster."));
    }

    [TestMethod]
    public async Task DuplicateCheckIn_ReturnsUnchangedRecord()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var first = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        fixture.TimeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc);
        var duplicate = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Qr,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: null);

        Assert.AreEqual(first.Record.Version, duplicate.Record.Version);
        Assert.AreEqual(first.Entries[0], duplicate.Entries[0]);
        Assert.HasCount(first.AuditEntries.Count, duplicate.AuditEntries);
    }

    [TestMethod]
    public async Task CheckInWithResult_AtomicallyReportsCreatedThenExisting()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        var created = await fixture.Service.CheckInWithResultAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");
        var existing = await fixture.Service.CheckInWithResultAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Qr,
            AttendanceTestData.ProctorId,
            credentialId: "credential-not-used-for-duplicate",
            manualReason: null);

        Assert.IsTrue(created.WasCreated);
        Assert.IsFalse(existing.WasCreated);
        Assert.AreEqual(created.Snapshot.Record.Version, existing.Snapshot.Record.Version);
        Assert.AreEqual(created.Snapshot.Entries[0], existing.Snapshot.Entries[0]);
    }

    [TestMethod]
    public async Task ApplyCutoff_ConvertsOnlyUnmarkedEntriesAndIsIdempotent()
    {
        var secondStudent = new AssignedStudent(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "2026-0002",
            "Ben Santos",
            "photos/ben.jpg");
        var fixture = CreateFixture(
            AttendanceTestData.StartsAtUtc.AddMinutes(-10),
            [AttendanceTestData.CreateDefinition().Students[0], secondStudent]);
        await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        fixture.TimeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddMinutes(15));
        var cutoff = await fixture.Service.ApplyCutoffAsync(
            fixture.Definition.Id,
            AttendanceTestData.ProctorId);
        var repeated = await fixture.Service.ApplyCutoffAsync(
            fixture.Definition.Id,
            AttendanceTestData.ProctorId);

        Assert.AreEqual(AttendanceStatus.Present, cutoff.Entries.Single(entry => entry.StudentId == AttendanceTestData.StudentId).Status);
        Assert.AreEqual(AttendanceStatus.PendingAbsence, cutoff.Entries.Single(entry => entry.StudentId == secondStudent.Id).Status);
        Assert.AreEqual(cutoff.Record.Version, repeated.Record.Version);
        Assert.HasCount(cutoff.AuditEntries.Count, repeated.AuditEntries);
    }

    [TestMethod]
    public async Task ConfirmAbsent_ChangesPendingEntryToAbsent()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(15));
        await fixture.Service.ApplyCutoffAsync(fixture.Definition.Id, AttendanceTestData.ProctorId);

        var snapshot = await fixture.Service.ConfirmAbsentAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceTestData.ProctorId);

        Assert.AreEqual(AttendanceStatus.Absent, snapshot.Entries[0].Status);
        Assert.AreEqual(AttendanceTestData.ProctorId, snapshot.Entries[0].ConfirmedByProctorId);
    }

    [TestMethod]
    public async Task PendingAndAbsentStudents_AreNotAdmissible()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(15));
        await fixture.Service.ApplyCutoffAsync(fixture.Definition.Id, AttendanceTestData.ProctorId);

        Assert.IsFalse(await fixture.Service.CanAdmitAsync(fixture.Definition.Id, AttendanceTestData.StudentId));

        await fixture.Service.ConfirmAbsentAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceTestData.ProctorId);

        Assert.IsFalse(await fixture.Service.CanAdmitAsync(fixture.Definition.Id, AttendanceTestData.StudentId));
    }

    [TestMethod]
    public async Task Correction_WithBlankReason_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Absent,
            " ",
            AttendanceTestData.ProctorId));
    }

    [TestMethod]
    public async Task Correction_AppendsAuditEntry()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var checkedIn = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        var corrected = await fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Late,
            "Arrival time was copied incorrectly.",
            AttendanceTestData.ProctorId);

        Assert.HasCount(checkedIn.AuditEntries.Count + 1, corrected.AuditEntries);
        var audit = corrected.AuditEntries[^1];
        Assert.AreEqual(AttendanceStatus.Present, audit.PriorStatus);
        Assert.AreEqual(AttendanceStatus.Late, audit.NewStatus);
        Assert.AreEqual("Arrival time was copied incorrectly.", audit.Reason);
        Assert.AreEqual(AttendanceTestData.ProctorId, audit.ProctorId);
    }

    [TestMethod]
    public async Task SameStatusCorrection_AppendsClericalAuditEntry()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        var checkedIn = await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        var corrected = await fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Present,
            "Confirmed the original entry after clerical review.",
            AttendanceTestData.ProctorId);

        Assert.AreEqual(AttendanceStatus.Present, corrected.Entries[0].Status);
        Assert.HasCount(checkedIn.AuditEntries.Count + 1, corrected.AuditEntries);
        Assert.AreEqual(AttendanceStatus.Present, corrected.AuditEntries[^1].PriorStatus);
        Assert.AreEqual(AttendanceStatus.Present, corrected.AuditEntries[^1].NewStatus);
    }

    [TestMethod]
    public async Task PostCutoffPromotion_WithoutReceiptEvidence_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(15));
        await fixture.Service.ApplyCutoffAsync(fixture.Definition.Id, AttendanceTestData.ProctorId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Present,
            "Student arrived after the attendance list was reviewed.",
            AttendanceTestData.ProctorId));
    }

    [TestMethod]
    public async Task PostCutoffAdmissionRevocation_IsAllowed()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");
        fixture.TimeProvider.SetUtcNow(AttendanceTestData.StartsAtUtc.AddMinutes(15));

        var corrected = await fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Absent,
            "Admission revoked after an identity discrepancy.",
            AttendanceTestData.ProctorId);

        Assert.AreEqual(AttendanceStatus.Absent, corrected.Entries[0].Status);
        Assert.IsFalse(await fixture.Service.CanAdmitAsync(fixture.Definition.Id, AttendanceTestData.StudentId));
    }

    [TestMethod]
    public async Task FinalizedRecord_IsReadOnly()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");
        fixture.TimeProvider.SetUtcNow(fixture.Definition.EndsAtUtc);
        await fixture.Service.FinalizeAsync(fixture.Definition.Id, AttendanceTestData.ProctorId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            null,
            "Duplicate attempt after finalization."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ApplyCutoffAsync(
            fixture.Definition.Id,
            AttendanceTestData.ProctorId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ConfirmAbsentAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceTestData.ProctorId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CorrectAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceStatus.Absent,
            "Attempted edit after finalization.",
            AttendanceTestData.ProctorId));
    }

    [TestMethod]
    public async Task Finalization_BeforeScheduledEnd_IsRejected()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.FinalizeAsync(
            fixture.Definition.Id,
            AttendanceTestData.ProctorId));
    }

    [TestMethod]
    public async Task Finalization_WithPendingOrUnmarkedEntry_IsRejected()
    {
        var unmarked = CreateFixture(AttendanceTestData.StartsAtUtc.AddHours(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unmarked.Service.FinalizeAsync(
            unmarked.Definition.Id,
            AttendanceTestData.ProctorId));

        var pending = CreateFixture(AttendanceTestData.StartsAtUtc.AddHours(2));
        await pending.Service.ApplyCutoffAsync(pending.Definition.Id, AttendanceTestData.ProctorId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.Service.FinalizeAsync(
            pending.Definition.Id,
            AttendanceTestData.ProctorId));
    }

    [TestMethod]
    public async Task Finalization_AfterEveryEntryIsResolved_Succeeds()
    {
        var fixture = CreateFixture(AttendanceTestData.StartsAtUtc.AddMinutes(-10));
        await fixture.Service.CheckInAsync(
            fixture.Definition.Id,
            AttendanceTestData.StudentId,
            AttendanceCheckInMethod.Manual,
            AttendanceTestData.ProctorId,
            credentialId: null,
            manualReason: "Identity confirmed from the roster.");
        fixture.TimeProvider.SetUtcNow(fixture.Definition.EndsAtUtc);

        var finalized = await fixture.Service.FinalizeAsync(
            fixture.Definition.Id,
            AttendanceTestData.ProctorId);

        Assert.IsTrue(finalized.IsFinalized);
        Assert.AreEqual(fixture.Definition.EndsAtUtc, finalized.Record.FinalizedAtUtc);
    }

    private static Fixture CreateFixture(
        DateTimeOffset utcNow,
        IReadOnlyList<AssignedStudent>? students = null)
    {
        var definition = AttendanceTestData.CreateDefinition(students);
        var provider = new InMemoryAttendanceSessionProvider([definition]);
        var store = new InMemoryAttendanceStore();
        var timeProvider = new TestTimeProvider(utcNow);
        return new Fixture(
            definition,
            store,
            timeProvider,
            new AttendanceService(provider, store, timeProvider));
    }

    private sealed record Fixture(
        AttendanceSessionDefinition Definition,
        InMemoryAttendanceStore Store,
        TestTimeProvider TimeProvider,
        AttendanceService Service);
}
