using System.Collections.ObjectModel;

namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentCreationOptions(
    IReadOnlyList<IncidentAssignment> Assignments,
    IReadOnlyList<IncidentCategory> Categories)
{
    private IReadOnlyList<IncidentAssignment> _assignments = Copy(Assignments);
    private IReadOnlyList<IncidentCategory> _categories = Copy(Categories);

    public IReadOnlyList<IncidentAssignment> Assignments
    {
        get => _assignments;
        init => _assignments = Copy(value);
    }

    public IReadOnlyList<IncidentCategory> Categories
    {
        get => _categories;
        init => _categories = Copy(value);
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
