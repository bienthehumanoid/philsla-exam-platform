namespace PhilSLA.ExamPlatform.Core.Attendance;

public interface IAttendanceStore
{
    Task<AttendanceSessionRecord?> LoadAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<AttendanceSessionRecord> CreateAsync(
        AttendanceSessionDefinition definition,
        CancellationToken cancellationToken = default);

    Task<AttendanceSessionRecord> SaveAsync(
        AttendanceSessionRecord record,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
