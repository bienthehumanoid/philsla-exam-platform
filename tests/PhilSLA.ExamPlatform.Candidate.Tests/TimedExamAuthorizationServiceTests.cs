using PhilSLA.ExamPlatform.Candidate.Examination;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class TimedExamAuthorizationServiceTests
{
    [TestMethod]
    public void Delay_ExposesConfiguredMvpAuthorizationDelay()
    {
        var service = new TimedExamAuthorizationService(
            TimeSpan.FromSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(5), service.Delay);
    }

    [TestMethod]
    public async Task WaitForAuthorizationAsync_HonorsCancellation()
    {
        var service = new TimedExamAuthorizationService(
            TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => service.WaitForAuthorizationAsync(cancellation.Token));
    }
}
