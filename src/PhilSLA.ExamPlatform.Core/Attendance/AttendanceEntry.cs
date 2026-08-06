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
    private DateTimeOffset? _receivedAtUtc =
        AttendanceTimestamp.RequireUtc(ReceivedAtUtc, nameof(ReceivedAtUtc));

    public DateTimeOffset? ReceivedAtUtc
    {
        get => _receivedAtUtc;
        init => _receivedAtUtc = AttendanceTimestamp.RequireUtc(value, nameof(ReceivedAtUtc));
    }
}
