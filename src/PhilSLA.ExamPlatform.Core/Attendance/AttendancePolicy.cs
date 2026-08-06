namespace PhilSLA.ExamPlatform.Core.Attendance;

public enum AttendanceCheckInDecision
{
    NotOpen,
    Present,
    Late,
    Closed
}

public sealed record AttendancePolicy(
    TimeSpan CheckInOpensBeforeStart,
    TimeSpan LateGracePeriod)
{
    private TimeSpan _checkInOpensBeforeStart =
        RequirePositive(CheckInOpensBeforeStart, nameof(CheckInOpensBeforeStart));
    private TimeSpan _lateGracePeriod =
        RequirePositive(LateGracePeriod, nameof(LateGracePeriod));

    public AttendanceCheckInDecision Classify(
        DateTimeOffset startsAtUtc,
        DateTimeOffset receivedAtUtc)
    {
        AttendanceTimestamp.RequireUtc(startsAtUtc, nameof(startsAtUtc));
        AttendanceTimestamp.RequireUtc(receivedAtUtc, nameof(receivedAtUtc));

        if (receivedAtUtc < startsAtUtc - CheckInOpensBeforeStart)
        {
            return AttendanceCheckInDecision.NotOpen;
        }

        if (receivedAtUtc < startsAtUtc)
        {
            return AttendanceCheckInDecision.Present;
        }

        return receivedAtUtc < startsAtUtc + LateGracePeriod
            ? AttendanceCheckInDecision.Late
            : AttendanceCheckInDecision.Closed;
    }

    public TimeSpan CheckInOpensBeforeStart
    {
        get => _checkInOpensBeforeStart;
        init => _checkInOpensBeforeStart = RequirePositive(value, nameof(CheckInOpensBeforeStart));
    }

    public TimeSpan LateGracePeriod
    {
        get => _lateGracePeriod;
        init => _lateGracePeriod = RequirePositive(value, nameof(LateGracePeriod));
    }

    private static TimeSpan RequirePositive(TimeSpan value, string parameterName) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
}

internal static class AttendanceTimestamp
{
    public static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("Timestamp must use a UTC offset.", parameterName);

    public static DateTimeOffset? RequireUtc(DateTimeOffset? value, string parameterName) =>
        value is null ? null : RequireUtc(value.Value, parameterName);
}
