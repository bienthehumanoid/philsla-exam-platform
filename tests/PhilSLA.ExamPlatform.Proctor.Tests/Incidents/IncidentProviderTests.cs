using PhilSLA.ExamPlatform.Proctor.Attendance;
using PhilSLA.ExamPlatform.Proctor.Incidents;
using PhilSLA.ExamPlatform.Proctor.Persistence;

namespace PhilSLA.ExamPlatform.Proctor.Tests.Incidents;

[TestClass]
public sealed class IncidentProviderTests
{
    [TestMethod]
    public async Task SeededCategories_AreStableActiveAndOrdered()
    {
        var first = await new SeededIncidentCategoryProvider().GetAsync();
        var second = await new SeededIncidentCategoryProvider().GetAsync();

        Assert.HasCount(6, first);
        CollectionAssert.AreEqual(first.Select(category => category.Id).ToArray(), second.Select(category => category.Id).ToArray());
        Assert.IsTrue(first.All(category => category.IsActive));
        Assert.AreEqual("Tab Switching", first[0].Name);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 6).ToArray(),
            first.Select(category => category.DisplayOrder).ToArray());
    }

    [TestMethod]
    public async Task AttendanceAdapter_ProjectsEveryAssignedCandidateWithSessionContext()
    {
        var provider = new SeededAttendanceSessionProvider();
        var adapter = new AttendanceIncidentAssignmentProvider(provider);
        var sessions = await provider.GetAssignedSessionsAsync(TemporaryProctorRepository.DemoProctorId);

        var assignments = await adapter.GetAssignedAsync(TemporaryProctorRepository.DemoProctorId);

        Assert.HasCount(sessions.Sum(session => session.Students.Count), assignments);
        var first = assignments.Single(item => item.CandidateId == sessions[0].Students[0].Id);
        Assert.AreEqual(sessions[0].Id, first.SessionId);
        Assert.AreEqual(sessions[0].Title, first.SessionTitle);
        Assert.AreEqual(sessions[0].Room, first.Room);
        Assert.AreEqual(sessions[0].Students[0].StudentNumber, first.StudentNumber);
        Assert.AreEqual(sessions[0].Students[0].FullName, first.CandidateName);
    }
}
