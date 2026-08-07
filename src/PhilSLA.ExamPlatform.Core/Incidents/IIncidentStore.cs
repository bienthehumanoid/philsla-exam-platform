namespace PhilSLA.ExamPlatform.Core.Incidents;

public interface IIncidentStore
{
    Task<IReadOnlyList<IncidentRecord>> LoadForSessionsAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default);

    Task<IncidentRecord> CreateAsync(
        IncidentRecord draft,
        IReadOnlyList<IncidentEvidenceUpload> uploads,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadEvidenceAsync(
        Guid incidentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
