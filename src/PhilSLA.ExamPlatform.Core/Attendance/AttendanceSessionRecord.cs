using System.Collections.ObjectModel;

namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionRecord(
    Guid SessionId,
    IReadOnlyList<AttendanceEntry> Entries,
    IReadOnlyList<AttendanceAuditEntry> AuditEntries,
    DateTimeOffset? FinalizedAtUtc,
    int Version)
{
    public IReadOnlyList<AttendanceEntry> Entries { get; init; } =
        ToReadOnly(Entries, nameof(Entries));

    public IReadOnlyList<AttendanceAuditEntry> AuditEntries { get; init; } =
        ToReadOnly(AuditEntries, nameof(AuditEntries));

    public DateTimeOffset? FinalizedAtUtc { get; init; } =
        AttendanceTimestamp.RequireUtc(FinalizedAtUtc, nameof(FinalizedAtUtc));

    public int Version { get; init; } =
        Version >= 0 ? Version : throw new ArgumentOutOfRangeException(nameof(Version));

    private static IReadOnlyList<T> ToReadOnly<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
