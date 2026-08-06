namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceEntry(
    Guid StudentId,
    AttendanceStatus Status,
    AttendanceCheckInMethod? CheckInMethod,
    DateTimeOffset? ReceivedAtUtc,
    string? CredentialId,
    string? ManualReason,
    Guid? ConfirmedByProctorId)
{
    public DateTimeOffset? ReceivedAtUtc { get; init; } =
        AttendanceTimestamp.RequireUtc(ReceivedAtUtc, nameof(ReceivedAtUtc));
}
