namespace PhilSLA.ExamPlatform.Candidate.Devices;

public interface IDeviceIdentityProvider
{
    Task<LocalDeviceIdentity> GetOrCreateAsync(
        CancellationToken cancellationToken = default);
}
