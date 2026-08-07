using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhilSLA.ExamPlatform.Candidate.Devices;

public sealed class DeviceIdentityProvider(
    IDeviceSecretStore secretStore,
    TimeProvider timeProvider) : IDeviceIdentityProvider
{
    private const string StorageKey = "philsla.device-identity.v1";
    private const int StorageFormatVersion = 1;
    private const string KeyAlgorithm = "ECDSA-P256";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private LocalDeviceIdentity? _identity;

    public async Task<LocalDeviceIdentity> GetOrCreateAsync(
        CancellationToken cancellationToken = default)
    {
        if (_identity is not null)
        {
            return _identity;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_identity is not null)
            {
                return _identity;
            }

            _identity = await LoadOrCreateAsync(cancellationToken);
            return _identity;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<LocalDeviceIdentity> LoadOrCreateAsync(
        CancellationToken cancellationToken)
    {
        string? storedValue;
        try
        {
            storedValue = await secretStore.GetAsync(
                StorageKey,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DeviceIdentityUnavailableException(
                "The protected device identity could not be read.",
                exception);
        }

        return storedValue is null
            ? await CreateAsync(cancellationToken)
            : Restore(storedValue);
    }

    private async Task<LocalDeviceIdentity> CreateAsync(
        CancellationToken cancellationToken)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var storedIdentity = new StoredDeviceIdentity(
            StorageFormatVersion,
            Guid.NewGuid(),
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            timeProvider.GetUtcNow());
        var serialized = JsonSerializer.Serialize(storedIdentity, JsonOptions);

        try
        {
            await secretStore.SetAsync(
                StorageKey,
                serialized,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DeviceIdentityUnavailableException(
                "The protected device identity could not be created.",
                exception);
        }

        return CreatePublicIdentity(storedIdentity, key);
    }

    private static LocalDeviceIdentity Restore(string serialized)
    {
        try
        {
            var storedIdentity = JsonSerializer.Deserialize<StoredDeviceIdentity>(
                serialized,
                JsonOptions);
            Validate(storedIdentity);

            var privateKey = Convert.FromBase64String(
                storedIdentity!.PrivateKeyPkcs8Base64);
            try
            {
                using var key = ECDsa.Create();
                key.ImportPkcs8PrivateKey(privateKey, out var bytesRead);
                if (bytesRead != privateKey.Length)
                {
                    throw new CryptographicException(
                        "The protected private key contains trailing data.");
                }

                return CreatePublicIdentity(storedIdentity, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateKey);
            }
        }
        catch (Exception exception) when (
            exception is JsonException or
            FormatException or
            CryptographicException or
            InvalidDataException)
        {
            throw new DeviceIdentityUnavailableException(
                "The protected device identity is corrupt or unsupported. " +
                "Do not create a replacement identity without authorization.",
                exception);
        }
    }

    private static void Validate(StoredDeviceIdentity? storedIdentity)
    {
        if (storedIdentity is null ||
            storedIdentity.FormatVersion != StorageFormatVersion ||
            storedIdentity.DeviceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(storedIdentity.PrivateKeyPkcs8Base64) ||
            storedIdentity.CreatedAtUtc == default)
        {
            throw new InvalidDataException(
                "The protected device identity is incomplete or unsupported.");
        }
    }

    private static LocalDeviceIdentity CreatePublicIdentity(
        StoredDeviceIdentity storedIdentity,
        ECDsa key)
    {
        var publicKey = key.ExportSubjectPublicKeyInfo();
        return new LocalDeviceIdentity(
            storedIdentity.DeviceId,
            KeyAlgorithm,
            Convert.ToBase64String(publicKey),
            Convert.ToHexString(SHA256.HashData(publicKey)),
            storedIdentity.CreatedAtUtc);
    }

    private sealed record StoredDeviceIdentity(
        int FormatVersion,
        Guid DeviceId,
        string PrivateKeyPkcs8Base64,
        DateTimeOffset CreatedAtUtc);
}
