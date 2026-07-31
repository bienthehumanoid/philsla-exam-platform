namespace PhilSLA.ExamPlatform.Candidate.Examination;

public sealed class SeededExamAssignmentProvider
    : IExamAssignmentProvider
{
    private static readonly ExamAssignment Assignment = new(
        "PhilSLA 2026 Global Assessment",
        new DateOnly(2026, 8, 15),
        "Ateneo de Manila University",
        "SEC Lecture Hall 1",
        "Ms. Ramos");

    public Task<ExamAssignment> GetAssignmentAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Assignment);
    }
}
