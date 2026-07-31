namespace PhilSLA.ExamPlatform.Candidate.Examination;

public interface IExamAuthorizationService
{
    Task WaitForAuthorizationAsync(
        CancellationToken cancellationToken = default);
}
