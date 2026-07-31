namespace PhilSLA.ExamPlatform.Candidate.Readiness;

public interface IDeviceReadinessService
{
    Task<DeviceReadinessReport> CheckAsync(
        CancellationToken cancellationToken = default);
}
