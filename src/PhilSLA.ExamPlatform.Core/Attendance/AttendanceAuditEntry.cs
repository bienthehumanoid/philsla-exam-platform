namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceAuditEntry(
    Guid EventId,
    Guid StudentId,
    AttendanceStatus PriorStatus,
    AttendanceStatus NewStatus,
    string Reason,
    Guid ProctorId,
    DateTimeOffset OccurredAtUtc)
{
    private DateTimeOffset _occurredAtUtc =
        AttendanceTimestamp.RequireUtc(OccurredAtUtc, nameof(OccurredAtUtc));

    public DateTimeOffset OccurredAtUtc
    {
        get => _occurredAtUtc;
        init => _occurredAtUtc = AttendanceTimestamp.RequireUtc(value, nameof(OccurredAtUtc));
    }
}
