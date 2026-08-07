using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

internal static class IncidentTestData
{
    public static readonly Guid ProctorId = Guid.Parse("be60020e-c818-4646-9b54-1f176924268d");
    public static readonly Guid SessionId = Guid.Parse("5a54fb75-b9af-401a-8150-75d396e2e99b");
    public static readonly Guid CandidateId = Guid.Parse("cae3d66c-2487-4593-9749-b1897bafad0a");
    public static readonly Guid CategoryId = Guid.Parse("115622db-f994-4e70-b67e-7dd3df182b02");
    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 8, 7, 2, 30, 0, TimeSpan.Zero);

    public static IncidentAssignment Assignment => new(
        SessionId,
        "PhilSLA Qualifying Examination",
        "Benitez Hall R101",
        CandidateId,
        "2026-0001",
        "Juan Carlos Villanueva");

    public static IncidentCategory Category => new(CategoryId, "Tab Switching", true, 0);

    public static IncidentCreateCommand Command => new(
        SessionId,
        CandidateId,
        CategoryId,
        IncidentSeverity.High,
        "Candidate repeatedly switched away from the examination window.");

    public static IncidentRecord CreateRecord(
        Guid? id = null,
        Guid? sessionId = null,
        IReadOnlyList<IncidentAttachment>? attachments = null) => new(
        id ?? Guid.Parse("efb81cc6-f861-462f-a8a9-33c91fdb4c06"),
        "INC-2026-001",
        sessionId ?? SessionId,
        Assignment.SessionTitle,
        Assignment.Room,
        CandidateId,
        Assignment.StudentNumber,
        Assignment.CandidateName,
        CategoryId,
        Category.Name,
        IncidentSeverity.High,
        Command.Description,
        IncidentReviewStatus.Pending,
        ProctorId,
        "Santiago Reyes",
        CreatedAtUtc,
        attachments ?? []);

    public static IncidentEvidenceUpload PngUpload(string name = "evidence.png") =>
        new(name, "image/png", 8, _ => Task.FromResult<Stream>(new MemoryStream(
            [137, 80, 78, 71, 13, 10, 26, 10])));
}
