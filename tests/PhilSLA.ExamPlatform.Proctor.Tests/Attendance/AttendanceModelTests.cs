using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class AttendanceModelTests
{
    [TestMethod]
    public void AuthoritativeTimestamps_RejectNonUtcOffsets()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.FromHours(8));
        var policy = new AttendancePolicy(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15));

        Assert.Throws<ArgumentException>(() => policy.Classify(nonUtc, AttendanceTestData.StartsAtUtc));
        Assert.Throws<ArgumentException>(() => policy.Classify(AttendanceTestData.StartsAtUtc, nonUtc));
        Assert.Throws<ArgumentException>(() => AttendanceTestData.CreateDefinition(startsAtUtc: nonUtc));
        Assert.Throws<ArgumentException>(() => AttendanceTestData.CreateDefinition(endsAtUtc: nonUtc.AddHours(2)));
        Assert.Throws<ArgumentException>(() => new AttendanceEntry(
            AttendanceTestData.StudentId,
            AttendanceStatus.Present,
            AttendanceCheckInMethod.Qr,
            nonUtc,
            "credential",
            null,
            AttendanceTestData.ProctorId));
        Assert.Throws<ArgumentException>(() => new AttendanceAuditEntry(
            Guid.NewGuid(),
            AttendanceTestData.StudentId,
            AttendanceStatus.Unmarked,
            AttendanceStatus.Present,
            "QR check-in",
            AttendanceTestData.ProctorId,
            nonUtc));
        Assert.Throws<ArgumentException>(() => new AttendanceSessionRecord(
            Guid.NewGuid(),
            [],
            [],
            nonUtc,
            0));
    }

    [TestMethod]
    public void PositionalModels_ExposeConstructorPropertiesAndDeconstructors()
    {
        var policy = new AttendancePolicy(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15));
        var definition = AttendanceTestData.CreateDefinition();

        var (checkInOpensBeforeStart, lateGracePeriod) = policy;
        var (
            id,
            assignedProctorId,
            title,
            examSet,
            room,
            startsAtUtc,
            endsAtUtc,
            definitionPolicy,
            students) = definition;

        Assert.AreEqual(TimeSpan.FromMinutes(30), checkInOpensBeforeStart);
        Assert.AreEqual(TimeSpan.FromMinutes(15), lateGracePeriod);
        Assert.AreEqual(definition.Id, id);
        Assert.AreEqual(definition.AssignedProctorId, assignedProctorId);
        Assert.AreEqual(definition.Title, title);
        Assert.AreEqual(definition.ExamSet, examSet);
        Assert.AreEqual(definition.Room, room);
        Assert.AreEqual(definition.StartsAtUtc, startsAtUtc);
        Assert.AreEqual(definition.EndsAtUtc, endsAtUtc);
        Assert.AreSame(definition.Policy, definitionPolicy);
        Assert.AreSame(definition.Students, students);
    }

    [TestMethod]
    public void CollectionProjections_CannotBeCastAndMutated()
    {
        var student = new AssignedStudent(
            AttendanceTestData.StudentId,
            "2026-0001",
            "Ana Reyes",
            "photos/ana.jpg");
        var definition = AttendanceTestData.CreateDefinition([student]);
        var entry = new AttendanceEntry(
            student.Id,
            AttendanceStatus.Unmarked,
            null,
            null,
            null,
            null,
            null);
        var record = new AttendanceSessionRecord(definition.Id, [entry], [], null, 0);
        var snapshot = new AttendanceSessionSnapshot(definition, record);

        Assert.IsFalse(definition.Students is AssignedStudent[]);
        Assert.IsFalse(definition.Students is List<AssignedStudent>);
        Assert.IsFalse(record.Entries is AttendanceEntry[]);
        Assert.IsFalse(record.Entries is List<AttendanceEntry>);
        Assert.IsFalse(record.AuditEntries is AttendanceAuditEntry[]);
        Assert.IsFalse(snapshot.Entries is AttendanceEntry[]);
        Assert.IsFalse(snapshot.Entries is List<AttendanceEntry>);
        Assert.IsFalse(snapshot.AuditEntries is AttendanceAuditEntry[]);
        Assert.Throws<NotSupportedException>(() => ((IList<AssignedStudent>)definition.Students)[0] = student with { FullName = "Changed" });
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEntry>)record.Entries)[0] = entry with { Status = AttendanceStatus.Present });
        Assert.Throws<NotSupportedException>(() => ((IList<AttendanceEntry>)snapshot.Entries)[0] = entry with { Status = AttendanceStatus.Late });
    }
}
