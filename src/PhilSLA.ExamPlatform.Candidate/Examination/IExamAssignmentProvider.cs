namespace PhilSLA.ExamPlatform.Candidate.Examination;

public interface IExamAssignmentProvider
{
    Task<ExamAssignment> GetAssignmentAsync(
        CancellationToken cancellationToken = default);
}
