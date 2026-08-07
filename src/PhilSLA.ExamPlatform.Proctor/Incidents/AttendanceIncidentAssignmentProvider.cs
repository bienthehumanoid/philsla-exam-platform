using PhilSLA.ExamPlatform.Core.Attendance;
using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Incidents;

public sealed class AttendanceIncidentAssignmentProvider(
    IAttendanceSessionProvider attendanceSessionProvider) : IIncidentAssignmentProvider
{
    public async Task<IReadOnlyList<IncidentAssignment>> GetAssignedAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await attendanceSessionProvider.GetAssignedSessionsAsync(
            proctorId,
            cancellationToken);
        return sessions
            .SelectMany(session => session.Students.Select(student => new IncidentAssignment(
                session.Id,
                session.Title,
                session.Room,
                student.Id,
                student.StudentNumber,
                student.FullName)))
            .ToArray();
    }
}
