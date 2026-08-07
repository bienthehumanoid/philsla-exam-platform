using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhilSLA.ExamPlatform.Contracts.Protocol;

public static class ContractJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] Serialize<TContract>(TContract contract)
        where TContract : IProtocolContract
    {
        ArgumentNullException.ThrowIfNull(contract);
        ContractProtocol.EnsureSupported(contract.ProtocolVersion);

        return JsonSerializer.SerializeToUtf8Bytes(contract, Options);
    }

    public static TContract Deserialize<TContract>(ReadOnlySpan<byte> json)
        where TContract : IProtocolContract
    {
        var contract = JsonSerializer.Deserialize<TContract>(json, Options)
            ?? throw new JsonException("The contract payload was empty.");

        ContractProtocol.EnsureSupported(contract.ProtocolVersion);
        return contract;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }
}
