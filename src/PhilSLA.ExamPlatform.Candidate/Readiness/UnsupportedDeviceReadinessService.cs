namespace PhilSLA.ExamPlatform.Candidate.Readiness;

public sealed class UnsupportedDeviceReadinessService
    : IDeviceReadinessService
{
    private static readonly ReadinessCheck UnsupportedCheck = new(
        ReadinessStatus.Unavailable,
        "Diagnostics are not available on this platform yet.");

    public Task<DeviceReadinessReport> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DeviceReadinessReport(
            UnsupportedCheck,
            UnsupportedCheck,
            UnsupportedCheck));
    }
}
