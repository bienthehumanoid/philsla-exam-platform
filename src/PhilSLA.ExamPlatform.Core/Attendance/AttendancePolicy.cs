namespace PhilSLA.ExamPlatform.Core.Attendance;

public enum AttendanceCheckInDecision
{
    NotOpen,
    Present,
    Late,
    Closed
}

public sealed record AttendancePolicy
{
    public AttendancePolicy(
        TimeSpan checkInOpensBeforeStart,
        TimeSpan lateGracePeriod)
    {
        if (checkInOpensBeforeStart <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(checkInOpensBeforeStart));
        }

        if (lateGracePeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lateGracePeriod));
        }

        CheckInOpensBeforeStart = checkInOpensBeforeStart;
        LateGracePeriod = lateGracePeriod;
    }

    public TimeSpan CheckInOpensBeforeStart { get; init; }

    public TimeSpan LateGracePeriod { get; init; }

    public AttendanceCheckInDecision Classify(
        DateTimeOffset startsAtUtc,
        DateTimeOffset receivedAtUtc)
    {
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
}
