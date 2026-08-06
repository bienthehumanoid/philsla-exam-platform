using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class AttendanceModelTests
{
    [TestMethod]
    public void SessionDefinition_RequiresAssignedSeatLabels()
    {
        var student = new AssignedStudent(
            AttendanceTestData.StudentId,
            "2026-0001",
            "Ana Reyes",
            " ",
            "photos/ana.jpg");

        var exception = Assert.Throws<ArgumentException>(() =>
            AttendanceTestData.CreateDefinition([student]));

        StringAssert.Contains(exception.Message, "seat label");
    }

    [TestMethod]
    public void SessionDefinition_RejectsDuplicateSeatLabelsIgnoringCase()
    {
        var students = new[]
        {
            new AssignedStudent(Guid.NewGuid(), "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg"),
            new AssignedStudent(Guid.NewGuid(), "2026-0002", "Ben Santos", "a01", "photos/ben.jpg")
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            AttendanceTestData.CreateDefinition(students));

        StringAssert.Contains(exception.Message, "Seat labels must be unique");
    }

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
            "A01",
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

    [TestMethod]
    public void WithExpressions_RejectNonUtcAuthoritativeTimestamps()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.FromHours(8));
        var definition = AttendanceTestData.CreateDefinition();
        var entry = new AttendanceEntry(AttendanceTestData.StudentId, AttendanceStatus.Unmarked, null, null, null, null, null);
        var audit = new AttendanceAuditEntry(Guid.NewGuid(), AttendanceTestData.StudentId, AttendanceStatus.Unmarked, AttendanceStatus.Present, "Check-in", AttendanceTestData.ProctorId, AttendanceTestData.StartsAtUtc);
        var record = new AttendanceSessionRecord(definition.Id, [entry], [audit], null, 0);

        Assert.Throws<ArgumentException>(() => definition with { StartsAtUtc = nonUtc });
        Assert.Throws<ArgumentException>(() => definition with { EndsAtUtc = nonUtc });
        Assert.Throws<ArgumentException>(() => entry with { ReceivedAtUtc = nonUtc });
        Assert.Throws<ArgumentException>(() => audit with { OccurredAtUtc = nonUtc });
        Assert.Throws<ArgumentException>(() => record with { FinalizedAtUtc = nonUtc });
    }

    [TestMethod]
    public void WithExpressions_DefensivelyWrapMutableCollections()
    {
        var student = new AssignedStudent(AttendanceTestData.StudentId, "2026-0001", "Ana Reyes", "A01", "photos/ana.jpg");
        var definition = AttendanceTestData.CreateDefinition([student]);
        var entry = new AttendanceEntry(student.Id, AttendanceStatus.Unmarked, null, null, null, null, null);
        var record = new AttendanceSessionRecord(definition.Id, [entry], [], null, 0);
        var replacementStudents = new[] { student };
        var replacementEntries = new[] { entry };
        var replacementAuditEntries = Array.Empty<AttendanceAuditEntry>();

        var updatedDefinition = definition with { Students = replacementStudents };
        var updatedRecord = record with
        {
            Entries = replacementEntries,
            AuditEntries = replacementAuditEntries
        };
        var snapshot = new AttendanceSessionSnapshot(updatedDefinition, updatedRecord);

        replacementStudents[0] = student with { FullName = "Changed outside model" };
        replacementEntries[0] = entry with { Status = AttendanceStatus.Present };

        Assert.IsFalse(updatedDefinition.Students is AssignedStudent[]);
        Assert.IsFalse(updatedRecord.Entries is AttendanceEntry[]);
        Assert.IsFalse(updatedRecord.AuditEntries is AttendanceAuditEntry[]);
        Assert.AreEqual("Ana Reyes", updatedDefinition.Students[0].FullName);
        Assert.AreEqual(AttendanceStatus.Unmarked, updatedRecord.Entries[0].Status);
        Assert.IsFalse(snapshot.Entries is AttendanceEntry[]);
        Assert.IsFalse(snapshot.AuditEntries is AttendanceAuditEntry[]);
    }
}
