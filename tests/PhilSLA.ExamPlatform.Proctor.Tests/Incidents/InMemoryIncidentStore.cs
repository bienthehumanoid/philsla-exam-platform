using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

internal sealed class InMemoryIncidentStore : IIncidentStore
{
    private readonly List<IncidentRecord> _records;

    public InMemoryIncidentStore(IEnumerable<IncidentRecord>? records = null)
    {
        _records = records?.ToList() ?? [];
    }

    public bool FailCreation { get; set; }

    public bool FailLoad { get; set; }

    public int CreateCalls { get; private set; }

    public byte[] EvidenceBytes { get; set; } = [137, 80, 78, 71];

    public Task<IReadOnlyList<IncidentRecord>> LoadForSessionsAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default) =>
        FailLoad
            ? throw new InvalidOperationException("Synthetic incident load failure.")
            : Task.FromResult<IReadOnlyList<IncidentRecord>>(_records
                .Where(record => sessionIds.Contains(record.SessionId))
                .OrderByDescending(record => record.CreatedAtUtc)
                .ToArray());

    public Task<IncidentRecord> CreateAsync(
        IncidentRecord draft,
        IReadOnlyList<IncidentEvidenceUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        if (FailCreation)
        {
            throw new InvalidOperationException("Synthetic incident save failure.");
        }

        var saved = draft with { DisplayId = $"INC-{draft.CreatedAtUtc.Year}-001" };
        _records.Add(saved);
        return Task.FromResult(saved);
    }

    public Task<byte[]> ReadEvidenceAsync(
        Guid incidentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EvidenceBytes.ToArray());
}
