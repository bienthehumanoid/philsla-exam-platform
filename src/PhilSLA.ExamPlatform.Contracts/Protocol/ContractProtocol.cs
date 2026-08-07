namespace PhilSLA.ExamPlatform.Contracts.Protocol;

public static class ContractProtocol
{
    public const int CurrentVersion = 1;

    internal static void EnsureSupported(int protocolVersion)
    {
        if (protocolVersion != CurrentVersion)
        {
            throw new UnsupportedProtocolVersionException(
                protocolVersion,
                CurrentVersion);
        }
    }
}
