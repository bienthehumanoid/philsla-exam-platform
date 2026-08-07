using System.Security.Cryptography;
using PhilSLA.ExamPlatform.Candidate.Devices;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

[TestClass]
public sealed class DeviceIdentityProviderTests
{
    private static readonly DateTimeOffset CreatedAt = new(
        2026,
        8,
        15,
        7,
        30,
        0,
        TimeSpan.Zero);

    [TestMethod]
    public async Task GetOrCreateAsync_PersistsAndRestoresStableIdentity()
    {
        var store = new InMemoryDeviceSecretStore();
        var firstProvider = CreateProvider(store);

        var created = await firstProvider.GetOrCreateAsync();
        var restored = await CreateProvider(store).GetOrCreateAsync();

        Assert.AreEqual(created, restored);
        Assert.AreNotEqual(Guid.Empty, created.DeviceId);
        Assert.AreEqual("ECDSA-P256", created.PublicKeyAlgorithm);
        Assert.AreEqual(CreatedAt, created.CreatedAtUtc);
        Assert.AreEqual(1, store.SetCount);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_ConcurrentCallsCreateOneIdentity()
    {
        var store = new InMemoryDeviceSecretStore();
        var provider = CreateProvider(store);

        var identities = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => provider.GetOrCreateAsync()));

        Assert.IsTrue(identities.All(identity => identity == identities[0]));
        Assert.AreEqual(1, store.SetCount);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_ReturnsValidPublicKeyAndThumbprint()
    {
        var identity = await CreateProvider(
            new InMemoryDeviceSecretStore()).GetOrCreateAsync();
        var publicKey = Convert.FromBase64String(identity.PublicKeyBase64);

        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);

        Assert.AreEqual(publicKey.Length, bytesRead);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(publicKey)),
            identity.PublicKeyThumbprintSha256Hex);
        Assert.HasCount(
            0,
            typeof(LocalDeviceIdentity)
                .GetProperties()
                .Where(property => property.Name.Contains(
                    "Private",
                    StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task GetOrCreateAsync_CorruptStateFailsWithoutReplacement()
    {
        var store = new InMemoryDeviceSecretStore("not-json");
        var provider = CreateProvider(store);

        var error = await Assert.ThrowsExactlyAsync<
            DeviceIdentityUnavailableException>(
            () => provider.GetOrCreateAsync());

        StringAssert.Contains(error.Message, "corrupt or unsupported");
        Assert.AreEqual(0, store.SetCount);
        Assert.AreEqual("not-json", store.Value);
    }

    private static DeviceIdentityProvider CreateProvider(
        InMemoryDeviceSecretStore store)
    {
        return new DeviceIdentityProvider(
            store,
            new TestTimeProvider(CreatedAt));
    }

    private sealed class InMemoryDeviceSecretStore(string? initialValue = null)
        : IDeviceSecretStore
    {
        private readonly object _sync = new();
        private string? _value = initialValue;

        public int SetCount { get; private set; }

        public string? Value
        {
            get
            {
                lock (_sync)
                {
                    return _value;
                }
            }
        }

        public Task<string?> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_value);
            }
        }

        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _value = value;
                SetCount++;
            }

            return Task.CompletedTask;
        }
    }
}
