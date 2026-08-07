using PhilSLA.ExamPlatform.Core.Incidents;
using PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

[TestClass]
public sealed class IncidentServiceTests
{
    [TestMethod]
    public async Task CreateAsync_ForcesPendingAndUsesTrustedSnapshots()
    {
        var (service, store) = CreateService();

        var created = await service.CreateAsync(
            IncidentTestData.Command with { Description = "  Candidate switched tabs.  " },
            IncidentTestData.ProctorId,
            "Santiago Reyes",
            []);

        Assert.AreEqual(IncidentReviewStatus.Pending, created.ReviewStatus);
        Assert.AreEqual("Candidate switched tabs.", created.Description);
        Assert.AreEqual(IncidentTestData.Assignment.CandidateName, created.CandidateName);
        Assert.AreEqual(IncidentTestData.Category.Name, created.CategoryName);
        Assert.AreEqual(IncidentTestData.CreatedAtUtc, created.CreatedAtUtc);
        Assert.AreEqual(1, store.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_RejectsCandidateOutsideAssignedSessions()
    {
        var (service, store) = CreateService();

        var exception = await Assert.ThrowsAsync<IncidentValidationException>(() =>
            service.CreateAsync(
                IncidentTestData.Command with { CandidateId = Guid.NewGuid() },
                IncidentTestData.ProctorId,
                "Santiago Reyes",
                []));

        StringAssert.Contains(exception.Message, "assigned");
        Assert.AreEqual(0, store.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_RejectsMissingOrInactiveCategory()
    {
        var inactive = IncidentTestData.Category with { IsActive = false };
        var (missingService, missingStore) = CreateService(categories: []);
        var (inactiveService, inactiveStore) = CreateService(categories: [inactive]);

        await Assert.ThrowsAsync<IncidentValidationException>(() => missingService.CreateAsync(
            IncidentTestData.Command,
            IncidentTestData.ProctorId,
            "Santiago Reyes",
            []));
        await Assert.ThrowsAsync<IncidentValidationException>(() => inactiveService.CreateAsync(
            IncidentTestData.Command,
            IncidentTestData.ProctorId,
            "Santiago Reyes",
            []));

        Assert.AreEqual(0, missingStore.CreateCalls);
        Assert.AreEqual(0, inactiveStore.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_RejectsMoreThanFiveAttachments()
    {
        var (service, store) = CreateService();
        var uploads = Enumerable.Range(1, 6)
            .Select(index => IncidentTestData.PngUpload($"evidence-{index}.png"))
            .ToArray();

        await Assert.ThrowsAsync<IncidentValidationException>(() => service.CreateAsync(
            IncidentTestData.Command,
            IncidentTestData.ProctorId,
            "Santiago Reyes",
            uploads));

        Assert.AreEqual(0, store.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_AcceptsEverySupportedSeverity()
    {
        foreach (var severity in Enum.GetValues<IncidentSeverity>())
        {
            var (service, _) = CreateService();
            var created = await service.CreateAsync(
                IncidentTestData.Command with { Severity = severity },
                IncidentTestData.ProctorId,
                "Santiago Reyes",
                []);

            Assert.AreEqual(severity, created.Severity);
        }
    }

    [TestMethod]
    public async Task LoadAssignedAsync_ReturnsOnlyAssignedSessionRecords()
    {
        var assigned = IncidentTestData.CreateRecord();
        var other = IncidentTestData.CreateRecord(id: Guid.NewGuid(), sessionId: Guid.NewGuid());
        var (service, _) = CreateService(records: [assigned, other]);

        var loaded = await service.LoadAssignedAsync(IncidentTestData.ProctorId);

        Assert.HasCount(1, loaded);
        Assert.AreEqual(assigned.Id, loaded.Single().Id);
    }

    [TestMethod]
    public async Task ReadEvidenceAsync_RejectsIncidentOutsideAssignedSessions()
    {
        var attachment = new IncidentAttachment(
            Guid.NewGuid(),
            "evidence.png",
            "image/png",
            8,
            "evidence.png",
            new string('A', 64));
        var record = IncidentTestData.CreateRecord(sessionId: Guid.NewGuid(), attachments: [attachment]);
        var (service, _) = CreateService(records: [record]);

        await Assert.ThrowsAsync<IncidentValidationException>(() => service.ReadEvidenceAsync(
            IncidentTestData.ProctorId,
            record.Id,
            attachment.Id));
    }

    [TestMethod]
    public async Task LoadCreationOptionsAsync_ReturnsAssignedCandidatesAndActiveCategories()
    {
        var inactive = new IncidentCategory(Guid.NewGuid(), "Inactive", false, 1);
        var (service, _) = CreateService(categories: [IncidentTestData.Category, inactive]);

        var options = await service.LoadCreationOptionsAsync(IncidentTestData.ProctorId);

        Assert.HasCount(1, options.Assignments);
        Assert.HasCount(1, options.Categories);
        Assert.AreEqual(IncidentTestData.Category.Id, options.Categories.Single().Id);
    }

    private static (IncidentService Service, InMemoryIncidentStore Store) CreateService(
        IReadOnlyList<IncidentAssignment>? assignments = null,
        IReadOnlyList<IncidentCategory>? categories = null,
        IReadOnlyList<IncidentRecord>? records = null)
    {
        var store = new InMemoryIncidentStore(records);
        var service = new IncidentService(
            new StubCategoryProvider(categories ?? [IncidentTestData.Category]),
            new StubAssignmentProvider(assignments ?? [IncidentTestData.Assignment]),
            store,
            new TestTimeProvider(IncidentTestData.CreatedAtUtc));
        return (service, store);
    }

    private sealed class StubCategoryProvider(IReadOnlyList<IncidentCategory> categories)
        : IIncidentCategoryProvider
    {
        public Task<IReadOnlyList<IncidentCategory>> GetAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(categories);
    }

    private sealed class StubAssignmentProvider(IReadOnlyList<IncidentAssignment> assignments)
        : IIncidentAssignmentProvider
    {
        public Task<IReadOnlyList<IncidentAssignment>> GetAssignedAsync(
            Guid proctorId,
            CancellationToken cancellationToken = default) => Task.FromResult(assignments);
    }
}
