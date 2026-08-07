using PhilSLA.ExamPlatform.Core.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

[TestClass]
public sealed class IncidentModelTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 7, 2, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void Category_RequiresIdentityAndName()
    {
        Assert.Throws<ArgumentException>(() => new IncidentCategory(Guid.Empty, "Tab Switching", true, 0));
        Assert.Throws<ArgumentException>(() => new IncidentCategory(Guid.NewGuid(), " ", true, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IncidentCategory(Guid.NewGuid(), "Tab Switching", true, -1));
    }

    [TestMethod]
    public void Assignment_RequiresCandidateAndSessionSnapshots()
    {
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { SessionId = Guid.Empty });
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { CandidateId = Guid.Empty });
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { SessionTitle = " " });
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { Room = " " });
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { StudentNumber = " " });
        Assert.Throws<ArgumentException>(() => CreateAssignment() with { CandidateName = " " });
    }

    [TestMethod]
    public void EvidenceUpload_AcceptsSupportedImagesAtSizeLimit()
    {
        var jpeg = CreateUpload("evidence.jpg", "image/jpeg", IncidentEvidenceUpload.MaximumBytes);
        var png = CreateUpload("evidence.png", "image/png", 1);

        Assert.AreEqual("evidence.jpg", jpeg.FileName);
        Assert.AreEqual("image/png", png.MediaType);
    }

    [TestMethod]
    public void EvidenceUpload_RejectsEmptyOversizedOrUnsupportedFiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateUpload("empty.png", "image/png", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateUpload("large.png", "image/png", IncidentEvidenceUpload.MaximumBytes + 1));
        Assert.Throws<ArgumentException>(() => CreateUpload("notes.pdf", "application/pdf", 10));
        Assert.Throws<ArgumentException>(() => CreateUpload("renamed.jpg", "image/png", 10));
    }

    [TestMethod]
    public void IncidentRecord_RequiresTrimmedDescriptionWithinLimit()
    {
        Assert.Throws<ArgumentException>(() => CreateRecord() with { Description = " " });
        Assert.Throws<ArgumentException>(() => CreateRecord() with
        {
            Description = new string('x', IncidentRecord.MaximumDescriptionLength + 1)
        });
    }

    [TestMethod]
    public void IncidentRecord_RequiresUtcCreationTime()
    {
        Assert.Throws<ArgumentException>(() => CreateRecord() with
        {
            CreatedAtUtc = CreatedAtUtc.ToOffset(TimeSpan.FromHours(8))
        });
    }

    [TestMethod]
    public void IncidentRecord_DefensivelyCopiesAttachments()
    {
        var attachments = new[] { CreateAttachment() };
        var record = CreateRecord(attachments);

        attachments[0] = attachments[0] with { OriginalFileName = "changed.png" };

        Assert.AreEqual("evidence.png", record.Attachments.Single().OriginalFileName);
        Assert.IsFalse(record.Attachments is IncidentAttachment[]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IncidentAttachment>)record.Attachments)[0] = CreateAttachment() with
            {
                OriginalFileName = "mutated.png"
            });
    }

    private static IncidentAssignment CreateAssignment() => new(
        Guid.Parse("5a54fb75-b9af-401a-8150-75d396e2e99b"),
        "PhilSLA Qualifying Examination",
        "Benitez Hall R101",
        Guid.Parse("cae3d66c-2487-4593-9749-b1897bafad0a"),
        "2026-0001",
        "Juan Carlos Villanueva");

    private static IncidentEvidenceUpload CreateUpload(string fileName, string mediaType, long length) =>
        new(fileName, mediaType, length, _ => Task.FromResult<Stream>(new MemoryStream([1])));

    private static IncidentAttachment CreateAttachment() => new(
        Guid.Parse("e2fe1ba8-95c1-4c2a-b40c-9e8356f6dba8"),
        "evidence.png",
        "image/png",
        68,
        "e2fe1ba8-95c1-4c2a-b40c-9e8356f6dba8.png",
        "D014EDC031656DD8E7721ED4E71DB3597A3E867A60213AE118AEF3B65B1E7D4A");

    private static IncidentRecord CreateRecord(IReadOnlyList<IncidentAttachment>? attachments = null) => new(
        Guid.Parse("efb81cc6-f861-462f-a8a9-33c91fdb4c06"),
        "INC-2026-001",
        CreateAssignment().SessionId,
        CreateAssignment().SessionTitle,
        CreateAssignment().Room,
        CreateAssignment().CandidateId,
        CreateAssignment().StudentNumber,
        CreateAssignment().CandidateName,
        Guid.Parse("115622db-f994-4e70-b67e-7dd3df182b02"),
        "Tab Switching",
        IncidentSeverity.High,
        "Candidate repeatedly switched away from the examination window.",
        IncidentReviewStatus.Pending,
        Guid.Parse("be60020e-c818-4646-9b54-1f176924268d"),
        "Santiago Reyes",
        CreatedAtUtc,
        attachments ?? []);
}
