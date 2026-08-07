using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Incidents;

public sealed class SeededIncidentCategoryProvider : IIncidentCategoryProvider
{
    private static readonly IReadOnlyList<IncidentCategory> Categories =
    [
        Category("71000000-0000-0000-0000-000000000001", "Tab Switching", 0),
        Category("71000000-0000-0000-0000-000000000002", "Unauthorized Materials", 1),
        Category("71000000-0000-0000-0000-000000000003", "Communication with Another Candidate", 2),
        Category("71000000-0000-0000-0000-000000000004", "Identity or Permit Concern", 3),
        Category("71000000-0000-0000-0000-000000000005", "Disruptive Conduct", 4),
        Category("71000000-0000-0000-0000-000000000006", "Manual Review Flag", 5)
    ];

    public Task<IReadOnlyList<IncidentCategory>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Categories);
    }

    private static IncidentCategory Category(string id, string name, int displayOrder) =>
        new(Guid.Parse(id), name, true, displayOrder);
}
