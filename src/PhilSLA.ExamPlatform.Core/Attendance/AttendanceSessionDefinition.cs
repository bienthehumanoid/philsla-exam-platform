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
    private DateTimeOffset _startsAtUtc =
        AttendanceTimestamp.RequireUtc(StartsAtUtc, nameof(StartsAtUtc));
    private DateTimeOffset _endsAtUtc =
        RequireUtcEndAfterStart(StartsAtUtc, EndsAtUtc);
    private AttendancePolicy _policy =
        Policy ?? throw new ArgumentNullException(nameof(Policy));
    private IReadOnlyList<AssignedStudent> _students =
        ToReadOnlyStudents(Students);

    public DateTimeOffset StartsAtUtc
    {
        get => _startsAtUtc;
        init
        {
            var startsAtUtc = AttendanceTimestamp.RequireUtc(value, nameof(StartsAtUtc));
            if (_endsAtUtc != default && startsAtUtc >= _endsAtUtc)
            {
                throw new ArgumentException("The session must end after it starts.", nameof(StartsAtUtc));
            }

            _startsAtUtc = startsAtUtc;
        }
    }

    public DateTimeOffset EndsAtUtc
    {
        get => _endsAtUtc;
        init => _endsAtUtc = RequireUtcEndAfterStart(_startsAtUtc, value);
    }

    public AttendancePolicy Policy
    {
        get => _policy;
        init => _policy = value ?? throw new ArgumentNullException(nameof(Policy));
    }

    public IReadOnlyList<AssignedStudent> Students
    {
        get => _students;
        init => _students = ToReadOnlyStudents(value);
    }

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
