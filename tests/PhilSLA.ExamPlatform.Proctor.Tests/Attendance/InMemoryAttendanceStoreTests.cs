using PhilSLA.ExamPlatform.Core.Attendance;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

[TestClass]
public sealed class InMemoryAttendanceStoreTests
{
    [TestMethod]
    public async Task SaveAsync_WithSameRecordVersion_IsRejectedWithoutChangingAggregate()
    {
        var store = new InMemoryAttendanceStore();
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
        var store = new InMemoryAttendanceStore();
        var created = await store.CreateAsync(AttendanceTestData.CreateDefinition());
        var malformed = AttendanceTestData.WithManualPresentAndCorrection(created);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            store.SaveAsync(malformed, created.Version));

        AssertUnchanged(created, await store.LoadAsync(created.SessionId));
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
}
