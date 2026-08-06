using System.Collections.ObjectModel;

namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionDefinition(
    Guid Id,
    Guid AssignedProctorId,
    string Title,
    string ExamSet,
    string Room,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    AttendancePolicy Policy,
    IReadOnlyList<AssignedStudent> Students)
{
    public DateTimeOffset StartsAtUtc { get; init; } =
        AttendanceTimestamp.RequireUtc(StartsAtUtc, nameof(StartsAtUtc));

    public DateTimeOffset EndsAtUtc { get; init; } =
        RequireUtcEndAfterStart(StartsAtUtc, EndsAtUtc);

    public AttendancePolicy Policy { get; init; } =
        Policy ?? throw new ArgumentNullException(nameof(Policy));

    public IReadOnlyList<AssignedStudent> Students { get; init; } =
        ToReadOnlyStudents(Students);

    private static DateTimeOffset RequireUtcEndAfterStart(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc)
    {
        AttendanceTimestamp.RequireUtc(endsAtUtc, nameof(EndsAtUtc));
        if (endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("The session must end after it starts.", nameof(EndsAtUtc));
        }

        return endsAtUtc;
    }

    private static IReadOnlyList<AssignedStudent> ToReadOnlyStudents(
        IReadOnlyList<AssignedStudent> students)
    {
        ArgumentNullException.ThrowIfNull(students);
        if (students.GroupBy(student => student.Id).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Student IDs must be unique.", nameof(Students));
        }

        return new ReadOnlyCollection<AssignedStudent>(students.ToArray());
    }
}
