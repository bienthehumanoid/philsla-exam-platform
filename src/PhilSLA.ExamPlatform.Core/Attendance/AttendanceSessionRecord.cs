namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionRecord
{
    public AttendanceSessionRecord(
        Guid sessionId,
        IReadOnlyList<AttendanceEntry> entries,
        IReadOnlyList<AttendanceAuditEntry> auditEntries,
        DateTimeOffset? finalizedAtUtc,
        int version)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(auditEntries);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        SessionId = sessionId;
        Entries = entries.ToArray();
        AuditEntries = auditEntries.ToArray();
        FinalizedAtUtc = finalizedAtUtc;
        Version = version;
    }

    public Guid SessionId { get; init; }
    public IReadOnlyList<AttendanceEntry> Entries { get; init; }
    public IReadOnlyList<AttendanceAuditEntry> AuditEntries { get; init; }
    public DateTimeOffset? FinalizedAtUtc { get; init; }
    public int Version { get; init; }
}
