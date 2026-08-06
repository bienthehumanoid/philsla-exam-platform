using System.Collections.ObjectModel;

namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionRecord(
    Guid SessionId,
    IReadOnlyList<AttendanceEntry> Entries,
    IReadOnlyList<AttendanceAuditEntry> AuditEntries,
    DateTimeOffset? FinalizedAtUtc,
    int Version)
{
    private IReadOnlyList<AttendanceEntry> _entries =
        ToReadOnly(Entries, nameof(Entries));
    private IReadOnlyList<AttendanceAuditEntry> _auditEntries =
        ToReadOnly(AuditEntries, nameof(AuditEntries));
    private DateTimeOffset? _finalizedAtUtc =
        AttendanceTimestamp.RequireUtc(FinalizedAtUtc, nameof(FinalizedAtUtc));
    private int _version =
        Version >= 0 ? Version : throw new ArgumentOutOfRangeException(nameof(Version));

    public IReadOnlyList<AttendanceEntry> Entries
    {
        get => _entries;
        init => _entries = ToReadOnly(value, nameof(Entries));
    }

    public IReadOnlyList<AttendanceAuditEntry> AuditEntries
    {
        get => _auditEntries;
        init => _auditEntries = ToReadOnly(value, nameof(AuditEntries));
    }

    public DateTimeOffset? FinalizedAtUtc
    {
        get => _finalizedAtUtc;
        init => _finalizedAtUtc = AttendanceTimestamp.RequireUtc(value, nameof(FinalizedAtUtc));
    }

    public int Version
    {
        get => _version;
        init => _version = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Version));
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
