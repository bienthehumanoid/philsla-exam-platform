using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

internal static class AttendanceTestData
{
    public static readonly DateTimeOffset StartsAtUtc =
        new(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);

    public static AttendanceSessionRecord WithManualPresentAndCorrection(
        AttendanceSessionRecord record) =>
        record with
        {
            Entries =
            [
                new AttendanceEntry(
                    StudentId,
                    AttendanceStatus.Present,
                    AttendanceCheckInMethod.Manual,
                    StartsAtUtc.AddMinutes(-5),
                    null,
                    "Manual check-in",
                    ProctorId)
            ],
            AuditEntries =
            [
                new AttendanceAuditEntry(
                    Guid.NewGuid(),
                    StudentId,
                    AttendanceStatus.Unmarked,
                    AttendanceStatus.Present,
                    "Manual check-in",
                    ProctorId,
                    StartsAtUtc.AddMinutes(-5)),
                new AttendanceAuditEntry(
                    Guid.NewGuid(),
                    StudentId,
                    AttendanceStatus.Present,
                    AttendanceStatus.Late,
                    "Corrected attendance status",
                    ProctorId,
                    StartsAtUtc)
            ],
            Version = record.Version + 2
        };

    public static Guid StudentId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static Guid ProctorId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static AttendanceSessionDefinition CreateDefinition(
        IReadOnlyList<AssignedStudent>? students = null,
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? endsAtUtc = null) =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ProctorId,
            "Civil Service Exam",
            "CSE-2026-A",
            "Room 101",
            startsAtUtc ?? StartsAtUtc,
            endsAtUtc ?? StartsAtUtc.AddHours(2),
            new AttendancePolicy(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15)),
            students ??
            [
                new AssignedStudent(StudentId, "2026-0001", "Ana Reyes", "photos/ana.jpg")
            ]);
}
