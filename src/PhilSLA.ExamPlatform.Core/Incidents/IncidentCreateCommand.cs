namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentCreateCommand(
    Guid SessionId,
    Guid CandidateId,
    Guid CategoryId,
    IncidentSeverity Severity,
    string Description);
