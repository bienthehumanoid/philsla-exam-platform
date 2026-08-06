namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionSnapshot
{
    public AttendanceSessionSnapshot(
        AttendanceSessionDefinition definition,
        AttendanceSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(record);

        if (record.SessionId != definition.Id)
        {
            throw new ArgumentException("The record must belong to the definition.", nameof(record));
        }

        var studentIds = definition.Students.Select(student => student.Id).Order().ToArray();
        var entryStudentIds = record.Entries.Select(entry => entry.StudentId).Order().ToArray();
        if (!studentIds.SequenceEqual(entryStudentIds))
        {
            throw new ArgumentException(
                "Record entries must correspond one-to-one with definition students.",
                nameof(record));
        }

        Definition = definition;
        Record = record;
    }

    public AttendanceSessionDefinition Definition { get; init; }
    public AttendanceSessionRecord Record { get; init; }

    public IReadOnlyList<AttendanceEntry> Entries => Record.Entries;
    public IReadOnlyList<AttendanceAuditEntry> AuditEntries => Record.AuditEntries;
    public int PresentCount => Entries.Count(entry => entry.Status == AttendanceStatus.Present);
    public int LateCount => Entries.Count(entry => entry.Status == AttendanceStatus.Late);
    public int AbsentCount => Entries.Count(entry => entry.Status == AttendanceStatus.Absent);
    public int PendingCount => Entries.Count(entry => entry.Status == AttendanceStatus.PendingAbsence);
    public int UnmarkedCount => Entries.Count(entry => entry.Status == AttendanceStatus.Unmarked);
    public bool IsFinalized => Record.FinalizedAtUtc.HasValue;
    public DateTimeOffset CheckInOpensAtUtc => Definition.StartsAtUtc - Definition.Policy.CheckInOpensBeforeStart;
    public DateTimeOffset CutoffAtUtc => Definition.StartsAtUtc + Definition.Policy.LateGracePeriod;
}
