using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

internal sealed class InMemoryAttendanceSessionProvider(
    IReadOnlyList<AttendanceSessionDefinition> sessions) : IAttendanceSessionProvider
{
    private readonly IReadOnlyList<AttendanceSessionDefinition> _sessions =
        sessions?.ToArray() ?? throw new ArgumentNullException(nameof(sessions));

    public Task<IReadOnlyList<AttendanceSessionDefinition>> GetAssignedSessionsAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AttendanceSessionDefinition> assigned = _sessions
            .Where(session => session.AssignedProctorId == proctorId)
            .ToArray();
        return Task.FromResult(assigned);
    }

    public Task<AttendanceSessionDefinition?> GetSessionAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.SingleOrDefault(session =>
            session.Id == sessionId && session.AssignedProctorId == proctorId));
    }
}
