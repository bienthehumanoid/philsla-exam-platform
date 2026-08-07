using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Incidents;
using PhilSLA.ExamPlatform.Infrastructure.Incidents;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

[TestClass]
public sealed class SqliteIncidentStoreTests
{
    private string _directory = null!;
    private string _databasePath = null!;
    private string _evidenceRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"philsla-incident-tests-{Guid.NewGuid():N}");
        _databasePath = Path.Combine(_directory, "incidents.db");
        _evidenceRoot = Path.Combine(_directory, "evidence");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateAsync_PersistsRecordAndEvidenceAcrossRestart()
    {
        var first = CreateStore();
        var saved = await first.CreateAsync(CreateDraft(), [IncidentTestData.PngUpload()]);

        var restarted = CreateStore();
        var loaded = (await restarted.LoadForSessionsAsync([IncidentTestData.SessionId])).Single();
        var bytes = await restarted.ReadEvidenceAsync(loaded.Id, loaded.Attachments.Single().Id);

        Assert.AreEqual("INC-2026-001", saved.DisplayId);
        Assert.AreEqual(saved.Id, loaded.Id);
        Assert.AreEqual(saved.Description, loaded.Description);
        Assert.AreEqual(saved.CandidateName, loaded.CandidateName);
        Assert.AreEqual(saved.CategoryName, loaded.CategoryName);
        Assert.AreEqual(saved.Severity, loaded.Severity);
        Assert.AreEqual(saved.ReviewStatus, loaded.ReviewStatus);
        Assert.AreEqual(saved.CreatedAtUtc, loaded.CreatedAtUtc);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes);
        Assert.AreEqual(64, loaded.Attachments.Single().Sha256.Length);
    }

    [TestMethod]
    public async Task CreateAsync_AllocatesUniqueYearlySequenceUnderConcurrency()
    {
        var store = CreateStore();
        var drafts = Enumerable.Range(0, 8)
            .Select(_ => CreateDraft(Guid.NewGuid()))
            .ToArray();

        var saved = await Task.WhenAll(drafts.Select(draft => store.CreateAsync(draft, [])));

        CollectionAssert.AreEquivalent(
            Enumerable.Range(1, 8).Select(value => $"INC-2026-{value:000}").ToArray(),
            saved.Select(record => record.DisplayId).ToArray());
    }

    [TestMethod]
    public async Task LoadForSessionsAsync_ReturnsOnlyRequestedSessionsNewestFirst()
    {
        var store = CreateStore();
        var older = await store.CreateAsync(CreateDraft(createdAtUtc: IncidentTestData.CreatedAtUtc.AddMinutes(-1)), []);
        var newer = await store.CreateAsync(CreateDraft(Guid.NewGuid()), []);
        await store.CreateAsync(CreateDraft(Guid.NewGuid(), Guid.NewGuid()), []);

        var loaded = await store.LoadForSessionsAsync([IncidentTestData.SessionId]);

        CollectionAssert.AreEqual(new[] { newer.Id, older.Id }, loaded.Select(record => record.Id).ToArray());
    }

    [TestMethod]
    public async Task CreateAsync_RejectsMismatchedImageSignatureWithoutPersistingRecord()
    {
        var store = CreateStore();
        var invalid = new IncidentEvidenceUpload(
            "fake.png",
            "image/png",
            8,
            _ => Task.FromResult<Stream>(new MemoryStream(new byte[8])));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateAsync(CreateDraft(), [invalid]));

        Assert.IsEmpty(await store.LoadForSessionsAsync([IncidentTestData.SessionId]));
        Assert.IsFalse(Directory.Exists(Path.Combine(_evidenceRoot, CreateDraft().Id.ToString("N"))));
    }

    [TestMethod]
    public async Task CreateAsync_WhenDatabaseInsertFails_RemovesPromotedEvidence()
    {
        var store = CreateStore();
        await store.LoadForSessionsAsync([IncidentTestData.SessionId]);
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TRIGGER fail_incident_insert
                BEFORE INSERT ON incident_records
                BEGIN
                    SELECT RAISE(ABORT, 'forced incident failure');
                END;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var draft = CreateDraft();
        await Assert.ThrowsExactlyAsync<SqliteException>(() =>
            store.CreateAsync(draft, [IncidentTestData.PngUpload()]));

        Assert.IsFalse(Directory.Exists(Path.Combine(_evidenceRoot, draft.Id.ToString("N"))));
        Assert.IsEmpty(await store.LoadForSessionsAsync([IncidentTestData.SessionId]));
    }

    [TestMethod]
    public async Task Initialization_RemovesOrphanEvidenceDirectories()
    {
        var orphan = Path.Combine(_evidenceRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(orphan);
        await File.WriteAllBytesAsync(Path.Combine(orphan, "orphan.png"), [1]);

        await CreateStore().LoadForSessionsAsync([IncidentTestData.SessionId]);

        Assert.IsFalse(Directory.Exists(orphan));
    }

    private SqliteIncidentStore CreateStore() => new(_databasePath, _evidenceRoot);

    private static IncidentRecord CreateDraft(
        Guid? id = null,
        Guid? sessionId = null,
        DateTimeOffset? createdAtUtc = null) =>
        IncidentTestData.CreateRecord(id, sessionId) with
        {
            DisplayId = string.Empty,
            CreatedAtUtc = createdAtUtc ?? IncidentTestData.CreatedAtUtc,
            Attachments = []
        };
}
