using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

internal sealed class InMemoryAttendanceStore : IAttendanceStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, AttendanceSessionRecord> _records = [];

    public Task<AttendanceSessionRecord?> LoadAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(
                _records.TryGetValue(sessionId, out var record) ? Clone(record) : null);
        }
    }

    public Task<AttendanceSessionRecord> CreateAsync(
        AttendanceSessionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_records.TryGetValue(definition.Id, out var record))
            {
                record = new AttendanceSessionRecord(
                    definition.Id,
                    definition.Students
                        .Select(student => new AttendanceEntry(
                            student.Id,
                            AttendanceStatus.Unmarked,
                            null,
                            null,
                            null,
                            null,
                            null))
                        .ToArray(),
                    [],
                    null,
                    0);
                _records.Add(definition.Id, Clone(record));
            }

            return Task.FromResult(Clone(record));
        }
    }

    public Task<AttendanceSessionRecord> SaveAsync(
        AttendanceSessionRecord record,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_records.TryGetValue(record.SessionId, out var stored) ||
                stored.Version != expectedVersion)
            {
                throw new InvalidOperationException("The attendance record was changed by another operation.");
            }

            var saved = Clone(record);
            _records[record.SessionId] = saved;
            return Task.FromResult(Clone(saved));
        }
    }

    private static AttendanceSessionRecord Clone(AttendanceSessionRecord record) =>
        new(
            record.SessionId,
            record.Entries.ToArray(),
            record.AuditEntries.ToArray(),
            record.FinalizedAtUtc,
            record.Version);
}
