namespace PhilSLA.ExamPlatform.Candidate.Readiness;

public sealed record ReadinessCheck(
    ReadinessStatus Status,
    string Message)
{
    public bool AllowsExamStart =>
        Status is ReadinessStatus.Ready or ReadinessStatus.Warning;
}
