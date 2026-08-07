namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentAttachment(
    Guid Id,
    string OriginalFileName,
    string MediaType,
    long Length,
    string RelativePath,
    string Sha256);
