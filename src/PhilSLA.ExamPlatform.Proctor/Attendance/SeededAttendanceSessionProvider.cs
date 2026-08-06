using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Attendance;

public sealed class SeededAttendanceSessionProvider : IAttendanceSessionProvider
{
    private static readonly TimeSpan PhilippineOffset = TimeSpan.FromHours(8);
    private static readonly AttendancePolicy StandardPolicy =
        new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15));

    private static readonly IReadOnlyList<AttendanceSessionDefinition> Sessions =
    [
        new(
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Empty,
            "PhilSLA - General admission",
            "PhilSLA General Admission 2026-A",
            "Benitez Hall R101, University",
            PhilippineTimeInUtc(2026, 6, 15, 8),
            PhilippineTimeInUtc(2026, 6, 15, 11),
            StandardPolicy,
            [
                Student("50000000-0000-0000-0000-000000000001", "2026-0001", "Ari Santos", 1),
                Student("50000000-0000-0000-0000-000000000002", "2026-0002", "Mika Flores", 2),
                Student("50000000-0000-0000-0000-000000000003", "2026-0003", "Nico Reyes", 3),
                Student("50000000-0000-0000-0000-000000000004", "2026-0004", "Lina Cruz", 1),
                Student("50000000-0000-0000-0000-000000000005", "2026-0005", "Tomas Garcia", 2),
                Student("50000000-0000-0000-0000-000000000006", "2026-0006", "Sela Ramos", 3)
            ]),
        new(
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            Guid.Empty,
            "PhilSLA - General admission",
            "PhilSLA General Admission 2026-A",
            "SEC Lecture Hall 1, Ateneo",
            PhilippineTimeInUtc(2026, 5, 22, 9),
            PhilippineTimeInUtc(2026, 5, 22, 12),
            StandardPolicy,
            [
                Student("50000000-0000-0000-0000-000000000007", "2026-0007", "Jori Mendoza", 1),
                Student("50000000-0000-0000-0000-000000000008", "2026-0008", "Bea Navarro", 2),
                Student("50000000-0000-0000-0000-000000000009", "2026-0009", "Enzo Lim", 3),
                Student("50000000-0000-0000-0000-000000000010", "2026-0010", "Kira Aquino", 1),
                Student("50000000-0000-0000-0000-000000000011", "2026-0011", "Paolo Torres", 2)
            ]),
        new(
            Guid.Parse("40000000-0000-0000-0000-000000000003"),
            Guid.Empty,
            "PhilSLA - General admission",
            "PhilSLA General Admission 2026-B",
            "Training Room 204, Makati",
            PhilippineTimeInUtc(2026, 5, 29, 13),
            PhilippineTimeInUtc(2026, 5, 29, 16),
            StandardPolicy,
            [
                Student("50000000-0000-0000-0000-000000000012", "2026-0012", "Cami Diaz", 3),
                Student("50000000-0000-0000-0000-000000000013", "2026-0013", "Luis Mercado", 1),
                Student("50000000-0000-0000-0000-000000000014", "2026-0014", "Rina Castillo", 2),
                Student("50000000-0000-0000-0000-000000000015", "2026-0015", "Theo Bautista", 3),
                Student("50000000-0000-0000-0000-000000000016", "2026-0016", "Mara Valdez", 1),
                Student("50000000-0000-0000-0000-000000000017", "2026-0017", "Ivo Domingo", 2),
                Student("50000000-0000-0000-0000-000000000018", "2026-0018", "Naya Soriano", 3),
                Student("50000000-0000-0000-0000-000000000019", "2026-0019", "Eli Ventura", 1)
            ])
    ];

    public Task<IReadOnlyList<AttendanceSessionDefinition>> GetAssignedSessionsAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AttendanceSessionDefinition> assigned = Sessions
            .Select(session => session with { AssignedProctorId = proctorId })
            .ToArray();
        return Task.FromResult(assigned);
    }

    public Task<AttendanceSessionDefinition?> GetSessionAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = Sessions.SingleOrDefault(candidate => candidate.Id == sessionId);
        return Task.FromResult(
            session is null ? null : session with { AssignedProctorId = proctorId });
    }

    private static DateTimeOffset PhilippineTimeInUtc(
        int year,
        int month,
        int day,
        int hour) =>
        new DateTimeOffset(year, month, day, hour, 0, 0, PhilippineOffset).ToUniversalTime();

    private static AssignedStudent Student(
        string id,
        string studentNumber,
        string fullName,
        int photoNumber) =>
        new(
            Guid.Parse(id),
            studentNumber,
            fullName,
            $"/images/candidates/candidate-{photoNumber:00}.svg");
}
