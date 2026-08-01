namespace PhilSLA.ExamPlatform.Core.Examinations;

public interface IExamDefinitionProvider
{
    Task<ExamDefinition> GetExamAsync(
        CancellationToken cancellationToken = default);
}
