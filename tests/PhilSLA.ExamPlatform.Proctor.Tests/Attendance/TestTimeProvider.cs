namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = RequireUtc(utcNow);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = RequireUtc(utcNow);

    private static DateTimeOffset RequireUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("Timestamp must use a UTC offset.", nameof(value));
}
