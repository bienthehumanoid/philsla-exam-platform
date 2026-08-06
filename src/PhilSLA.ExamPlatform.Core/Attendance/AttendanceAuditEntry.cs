namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceAuditEntry(
    Guid EventId,
    Guid StudentId,
    AttendanceStatus PriorStatus,
    AttendanceStatus NewStatus,
    string Reason,
    Guid ProctorId,
    DateTimeOffset OccurredAtUtc);
