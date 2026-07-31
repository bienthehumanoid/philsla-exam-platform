namespace PhilSLA.ExamPlatform.Candidate.Examination;

public sealed class TimedExamAuthorizationService(TimeSpan delay)
    : IExamAuthorizationService
{
    public TimeSpan Delay { get; } = delay;

    public Task WaitForAuthorizationAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Delay(Delay, cancellationToken);
    }
}
