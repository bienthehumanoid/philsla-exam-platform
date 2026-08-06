using Microsoft.Data.Sqlite;
using PhilSLA.ExamPlatform.Core.Attendance;
using PhilSLA.ExamPlatform.Infrastructure.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class SqliteAttendanceStoreTests
{
    private string _databasePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"philsla-attendance-tests-{Guid.NewGuid():N}.db");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfPresent(_databasePath);
        DeleteIfPresent($"{_databasePath}-wal");
        DeleteIfPresent($"{_databasePath}-shm");
    }

    [TestMethod]
    public async Task SavedAttendanceAndAuditHistory_SurviveStoreRestart()
    {
        var first = new SqliteAttendanceStore(_databasePath);
        var created = await first.CreateAsync(AttendanceTestData.CreateDefinition());
        var changed = CreateManualAttendance(created);
        await first.SaveAsync(changed, created.Version);

        var restarted = new SqliteAttendanceStore(_databasePath);
        var restored = await restarted.LoadAsync(created.SessionId);

        Assert.IsNotNull(restored);
        Assert.AreEqual(AttendanceStatus.Present, restored.Entries[0].Status);
        Assert.AreEqual(AttendanceCheckInMethod.Manual, restored.Entries[0].CheckInMethod);
        Assert.AreEqual(AttendanceTestData.StartsAtUtc.AddMinutes(-5), restored.Entries[0].ReceivedAtUtc);
        Assert.AreEqual("Printed permit was unavailable.", restored.Entries[0].ManualReason);
        Assert.AreEqual(AttendanceTestData.ProctorId, restored.Entries[0].ConfirmedByProctorId);
        Assert.HasCount(1, restored.AuditEntries);
        Assert.AreEqual(changed.AuditEntries[0], restored.AuditEntries[0]);
        Assert.AreEqual(changed.Version, restored.Version);
    }

    [TestMethod]
    public async Task CreateAsync_WhenSessionAlreadyExists_ReturnsExistingAttendance()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var changed = CreateManualAttendance(created);
        await store.SaveAsync(changed, created.Version);

        var duplicate = await store.CreateAsync(AttendanceTestData.CreateDefinition());

        Assert.AreEqual(changed.Version, duplicate.Version);
        Assert.AreEqual(changed.Entries[0], duplicate.Entries[0]);
        Assert.HasCount(1, duplicate.AuditEntries);
        Assert.AreEqual(changed.AuditEntries[0], duplicate.AuditEntries[0]);
    }

    [TestMethod]
    public async Task SaveAsync_WithStaleExpectedVersion_IsRejectedWithoutChangingRecord()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var changed = CreateManualAttendance(created);
        await store.SaveAsync(changed, created.Version);
        var staleChange = changed with
        {
            Entries =
            [
                changed.Entries[0] with { Status = AttendanceStatus.Late }
            ],
            Version = changed.Version + 1
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.SaveAsync(staleChange, created.Version));

        var restored = await store.LoadAsync(created.SessionId);
        Assert.IsNotNull(restored);
        Assert.AreEqual(AttendanceStatus.Present, restored.Entries[0].Status);
        Assert.AreEqual(changed.Version, restored.Version);
    }

    [TestMethod]
    public async Task SaveAsync_WithSameRecordVersion_IsRejectedWithoutChangingAggregate()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var malformed = AttendanceTestData.WithManualPresentAndCorrection(created) with
        {
            Version = created.Version
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            store.SaveAsync(malformed, created.Version));

        AssertUnchanged(created, await store.LoadAsync(created.SessionId));
    }

    [TestMethod]
    public async Task SaveAsync_WithSkippedRecordVersion_IsRejectedWithoutChangingAggregate()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var malformed = AttendanceTestData.WithManualPresentAndCorrection(created);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            store.SaveAsync(malformed, created.Version));

        AssertUnchanged(created, await store.LoadAsync(created.SessionId));
    }

    [TestMethod]
    public async Task FinalizedTimestamp_SurvivesStoreRestart()
    {
        var first = new SqliteAttendanceStore(_databasePath);
        var created = await first.CreateAsync(AttendanceTestData.CreateDefinition());
        var finalizedAtUtc = AttendanceTestData.StartsAtUtc.AddHours(2);
        var finalized = created with
        {
            FinalizedAtUtc = finalizedAtUtc,
            Version = created.Version + 1
        };
        await first.SaveAsync(finalized, created.Version);

        var restarted = new SqliteAttendanceStore(_databasePath);
        var restored = await restarted.LoadAsync(created.SessionId);

        Assert.IsNotNull(restored);
        Assert.AreEqual(finalizedAtUtc, restored.FinalizedAtUtc);
    }

    [TestMethod]
    public async Task QrCredentialId_SurvivesStoreRestart()
    {
        var first = new SqliteAttendanceStore(_databasePath);
        var created = await first.CreateAsync(AttendanceTestData.CreateDefinition());
        var receivedAtUtc = AttendanceTestData.StartsAtUtc.AddMinutes(-10);
        var checkedIn = created with
        {
            Entries =
            [
                new AttendanceEntry(
                    AttendanceTestData.StudentId,
                    AttendanceStatus.Present,
                    AttendanceCheckInMethod.Qr,
                    receivedAtUtc,
                    "credential-2026-0001",
                    null,
                    null)
            ],
            Version = created.Version + 1
        };
        await first.SaveAsync(checkedIn, created.Version);

        var restarted = new SqliteAttendanceStore(_databasePath);
        var restored = await restarted.LoadAsync(created.SessionId);

        Assert.IsNotNull(restored);
        Assert.AreEqual("credential-2026-0001", restored.Entries[0].CredentialId);
        Assert.AreEqual(receivedAtUtc, restored.Entries[0].ReceivedAtUtc);
    }

    [TestMethod]
    public async Task InitializedDatabase_UsesWalJournalMode()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        await store.LoadAsync(AttendanceTestData.CreateDefinition().Id);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        var journalMode = (string?)await command.ExecuteScalarAsync();

        Assert.AreEqual("wal", journalMode);
    }

    [TestMethod]
    public async Task FailedRetry_DoesNotDuplicateAuditRows()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var changed = CreateManualAttendance(created);
        await store.SaveAsync(changed, created.Version);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.SaveAsync(changed, created.Version));

        var recoveredRetry = changed with { Version = changed.Version + 1 };
        await store.SaveAsync(recoveredRetry, changed.Version);

        var restarted = new SqliteAttendanceStore(_databasePath);
        var restored = await restarted.LoadAsync(created.SessionId);
        Assert.IsNotNull(restored);
        Assert.HasCount(1, restored.AuditEntries);
        Assert.AreEqual(recoveredRetry.Version, restored.Version);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM attendance_audit WHERE session_id = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", created.SessionId.ToString());
        Assert.AreEqual(1L, await command.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task SaveAsync_WhenAuditInsertFails_RollsBackEntireAggregate()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());

        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TRIGGER fail_attendance_audit
                BEFORE INSERT ON attendance_audit
                BEGIN
                    SELECT RAISE(ABORT, 'forced audit failure');
                END;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var changed = CreateManualAttendance(created) with
        {
            FinalizedAtUtc = AttendanceTestData.StartsAtUtc.AddHours(2)
        };

        await Assert.ThrowsExactlyAsync<SqliteException>(
            () => store.SaveAsync(changed, created.Version));

        var restored = await store.LoadAsync(created.SessionId);
        Assert.IsNotNull(restored);
        Assert.AreEqual(created.Version, restored.Version);
        Assert.IsNull(restored.FinalizedAtUtc);
        Assert.AreEqual(AttendanceStatus.Unmarked, restored.Entries[0].Status);
        Assert.IsEmpty(restored.AuditEntries);
    }

    [TestMethod]
    public async Task LoadAsync_WhileWriteIsOpen_ReadsPriorWalSnapshotWithoutBlocking()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());

        await using var writer = new SqliteConnection($"Data Source={_databasePath}");
        await writer.OpenAsync();
        await using var transaction = await writer.BeginTransactionAsync();
        await using (var command = writer.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                UPDATE attendance_sessions
                SET version = 1
                WHERE session_id = $sessionId;

                INSERT INTO attendance_audit (
                    event_id,
                    session_id,
                    student_id,
                    prior_status,
                    new_status,
                    reason,
                    proctor_id,
                    occurred_at_utc
                )
                VALUES (
                    '66666666-6666-6666-6666-666666666666',
                    $sessionId,
                    $studentId,
                    'Unmarked',
                    'Present',
                    'Concurrent write.',
                    $proctorId,
                    '2026-08-06T07:55:00.0000000+00:00'
                );
                """;
            command.Parameters.AddWithValue("$sessionId", created.SessionId.ToString());
            command.Parameters.AddWithValue("$studentId", AttendanceTestData.StudentId.ToString());
            command.Parameters.AddWithValue("$proctorId", AttendanceTestData.ProctorId.ToString());
            await command.ExecuteNonQueryAsync();
        }

        var loadTask = Task.Run(() => store.LoadAsync(created.SessionId));
        var completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(1)));
        var completedBeforeWriter = ReferenceEquals(completed, loadTask);
        await transaction.CommitAsync();
        var restored = await loadTask;

        Assert.IsTrue(
            completedBeforeWriter,
            "The attendance read waited for the open WAL writer transaction.");
        Assert.IsNotNull(restored);
        Assert.AreEqual(created.Version, restored.Version);
        Assert.IsEmpty(restored.AuditEntries);
    }

    [TestMethod]
    public async Task LoadAsync_WhenSaveCommitsAfterHeaderRead_ReturnsPreCommitAggregateSnapshot()
    {
        var store = new SqliteAttendanceStore(_databasePath);
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var headerRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRecovery = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var recoveringStore = new SqliteAttendanceStore(
            _databasePath,
            async cancellationToken =>
            {
                headerRead.TrySetResult(true);
                await continueRecovery.Task.WaitAsync(cancellationToken);
            });

        var loadTask = Task.Run(() => recoveringStore.LoadAsync(created.SessionId));
        var changed = CreateManualAttendance(created);
        try
        {
            await headerRead.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await store.SaveAsync(changed, created.Version);
        }
        finally
        {
            continueRecovery.TrySetResult(true);
        }

        var restored = await loadTask;

        Assert.IsNotNull(restored);
        Assert.AreEqual(created.Version, restored.Version);
        Assert.AreEqual(AttendanceStatus.Unmarked, restored.Entries[0].Status);
        Assert.IsEmpty(restored.AuditEntries);
    }

    private static AttendanceSessionRecord CreateManualAttendance(
        AttendanceSessionRecord created)
    {
        var occurredAtUtc = AttendanceTestData.StartsAtUtc.AddMinutes(-5);
        return created with
        {
            Entries =
            [
                new AttendanceEntry(
                    AttendanceTestData.StudentId,
                    AttendanceStatus.Present,
                    AttendanceCheckInMethod.Manual,
                    occurredAtUtc,
                    null,
                    "Printed permit was unavailable.",
                    AttendanceTestData.ProctorId)
            ],
            AuditEntries =
            [
                new AttendanceAuditEntry(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    AttendanceTestData.StudentId,
                    AttendanceStatus.Unmarked,
                    AttendanceStatus.Present,
                    "Printed permit was unavailable.",
                    AttendanceTestData.ProctorId,
                    occurredAtUtc)
            ],
            Version = created.Version + 1
        };
    }

    private static void AssertUnchanged(
        AttendanceSessionRecord expected,
        AttendanceSessionRecord? actual)
    {
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.Version, actual.Version);
        Assert.AreEqual(AttendanceStatus.Unmarked, actual.Entries[0].Status);
        Assert.IsNull(actual.FinalizedAtUtc);
        Assert.IsEmpty(actual.AuditEntries);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
