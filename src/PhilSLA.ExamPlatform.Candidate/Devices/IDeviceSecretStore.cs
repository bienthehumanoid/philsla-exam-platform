namespace PhilSLA.ExamPlatform.Candidate.Devices;

public interface IDeviceSecretStore
{
    Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);
}
