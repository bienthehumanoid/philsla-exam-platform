using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class AttendancePolicyTests
{
    [TestMethod]
    [DataRow(-31, AttendanceCheckInDecision.NotOpen)]
    [DataRow(-30, AttendanceCheckInDecision.Present)]
    [DataRow(-1, AttendanceCheckInDecision.Present)]
    [DataRow(0, AttendanceCheckInDecision.Late)]
    [DataRow(14, AttendanceCheckInDecision.Late)]
    [DataRow(15, AttendanceCheckInDecision.Closed)]
    public void Classify_UsesExactConfiguredBoundaries(
        int minutesFromStart,
        AttendanceCheckInDecision expected)
    {
        var policy = new AttendancePolicy(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(15));
        var receivedAt = AttendanceTestData.StartsAtUtc.AddMinutes(minutesFromStart);

        Assert.AreEqual(
            expected,
            policy.Classify(AttendanceTestData.StartsAtUtc, receivedAt));
    }

    [TestMethod]
    [DataRow(0, 15)]
    [DataRow(-1, 15)]
    [DataRow(30, 0)]
    [DataRow(30, -1)]
    public void Constructor_RejectsNonPositivePeriods(
        int openingMinutes,
        int graceMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AttendancePolicy(
                TimeSpan.FromMinutes(openingMinutes),
                TimeSpan.FromMinutes(graceMinutes)));
    }

    [TestMethod]
    public void Definition_RejectsDuplicateStudentIds()
    {
        var student = new AssignedStudent(
            AttendanceTestData.StudentId,
            "2026-0001",
            "Ana Reyes",
            "A01",
            "photos/ana.jpg");

        Assert.Throws<ArgumentException>(() =>
            AttendanceTestData.CreateDefinition([student, student with { StudentNumber = "2026-0002" }]));
    }

    [TestMethod]
    public void Definition_RejectsNonPositiveSessionDuration()
    {
        Assert.Throws<ArgumentException>(() =>
            AttendanceTestData.CreateDefinition(
                startsAtUtc: AttendanceTestData.StartsAtUtc,
                endsAtUtc: AttendanceTestData.StartsAtUtc));
    }

    [TestMethod]
    public void Snapshot_RejectsEntriesThatDoNotMatchDefinitionStudents()
    {
        var definition = AttendanceTestData.CreateDefinition();
        var record = new AttendanceSessionRecord(
            definition.Id,
            [new AttendanceEntry(Guid.NewGuid(), AttendanceStatus.Unmarked, null, null, null, null, null)],
            [],
            null,
            0);

        Assert.Throws<ArgumentException>(() =>
            new AttendanceSessionSnapshot(definition, record));
    }

    [TestMethod]
    public void Record_RejectsNegativeVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AttendanceSessionRecord(Guid.NewGuid(), [], [], null, -1));
    }
}
