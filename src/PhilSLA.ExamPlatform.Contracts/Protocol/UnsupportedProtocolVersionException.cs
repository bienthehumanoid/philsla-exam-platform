namespace PhilSLA.ExamPlatform.Contracts.Protocol;

public sealed class UnsupportedProtocolVersionException(
    int actualVersion,
    int supportedVersion)
    : Exception(
        $"Protocol version {actualVersion} is not supported. " +
        $"This application supports version {supportedVersion}.")
{
    public int ActualVersion { get; } = actualVersion;

    public int SupportedVersion { get; } = supportedVersion;
}
