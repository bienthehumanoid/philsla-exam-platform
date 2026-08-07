namespace PhilSLA.ExamPlatform.Core.Incidents;

public interface IIncidentCategoryProvider
{
    Task<IReadOnlyList<IncidentCategory>> GetAsync(
        CancellationToken cancellationToken = default);
}
