namespace PhilSLA.ExamPlatform.Candidate.Readiness;

public sealed record DeviceReadinessReport(
    ReadinessCheck Camera,
    ReadinessCheck Microphone,
    ReadinessCheck Network)
{
    public bool CanStartExam =>
        Camera.AllowsExamStart &&
        Microphone.AllowsExamStart &&
        Network.AllowsExamStart;
}
