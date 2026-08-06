namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceSessionDefinition
{
    public AttendanceSessionDefinition(
        Guid id,
        Guid assignedProctorId,
        string title,
        string examSet,
        string room,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        AttendancePolicy policy,
        IReadOnlyList<AssignedStudent> students)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(students);

        if (endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("The session must end after it starts.", nameof(endsAtUtc));
        }

        if (students.GroupBy(student => student.Id).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Student IDs must be unique.", nameof(students));
        }

        Id = id;
        AssignedProctorId = assignedProctorId;
        Title = title;
        ExamSet = examSet;
        Room = room;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Policy = policy;
        Students = students.ToArray();
    }

    public Guid Id { get; init; }
    public Guid AssignedProctorId { get; init; }
    public string Title { get; init; }
    public string ExamSet { get; init; }
    public string Room { get; init; }
    public DateTimeOffset StartsAtUtc { get; init; }
    public DateTimeOffset EndsAtUtc { get; init; }
    public AttendancePolicy Policy { get; init; }
    public IReadOnlyList<AssignedStudent> Students { get; init; }
}
