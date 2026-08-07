using Microsoft.Maui.Storage;

namespace PhilSLA.ExamPlatform.Candidate.Devices;

public sealed class MauiDeviceSecretStore(ISecureStorage secureStorage)
    : IDeviceSecretStore
{
    public async Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await secureStorage.GetAsync(key);
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    public async Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await secureStorage.SetAsync(key, value);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
