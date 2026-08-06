namespace PhilSLA.ExamPlatform.Core.Attendance;

public interface IAttendanceSessionProvider
{
    Task<IReadOnlyList<AttendanceSessionDefinition>> GetAssignedSessionsAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default);

    Task<AttendanceSessionDefinition?> GetSessionAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default);
}
