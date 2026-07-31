#if WINDOWS
using Microsoft.Maui.Networking;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;

namespace PhilSLA.ExamPlatform.Candidate.Readiness;

public sealed class WindowsDeviceReadinessService(
    IConnectivity connectivity) : IDeviceReadinessService
{
    public async Task<DeviceReadinessReport> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var camera = await CheckCameraAsync(cancellationToken);
        var microphone = await CheckMicrophoneAsync(cancellationToken);
        var network = CheckNetwork();

        return new DeviceReadinessReport(camera, microphone, network);
    }

    private static async Task<ReadinessCheck> CheckCameraAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = await DeviceInformation.FindAllAsync(
                DeviceClass.VideoCapture);
            cancellationToken.ThrowIfCancellationRequested();

            var device = devices.FirstOrDefault();
            if (device is null)
            {
                return new ReadinessCheck(
                    ReadinessStatus.Failed,
                    "No camera detected.");
            }

            using var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = device.Id,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await capture.InitializeAsync(settings);
            cancellationToken.ThrowIfCancellationRequested();

            return new ReadinessCheck(
                ReadinessStatus.Ready,
                $"Camera ready: {device.Name}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new ReadinessCheck(
                ReadinessStatus.Failed,
                "Camera access is blocked in Windows privacy settings.");
        }
        catch
        {
            return new ReadinessCheck(
                ReadinessStatus.Failed,
                "Camera unavailable or in use.");
        }
    }

    private static async Task<ReadinessCheck> CheckMicrophoneAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = await DeviceInformation.FindAllAsync(
                DeviceClass.AudioCapture);
            cancellationToken.ThrowIfCancellationRequested();

            var device = devices.FirstOrDefault();
            if (device is null)
            {
                return new ReadinessCheck(
                    ReadinessStatus.Failed,
                    "No microphone detected.");
            }

            using var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                AudioDeviceId = device.Id,
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await capture.InitializeAsync(settings);
            cancellationToken.ThrowIfCancellationRequested();

            return new ReadinessCheck(
                ReadinessStatus.Ready,
                $"Microphone ready: {device.Name}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new ReadinessCheck(
                ReadinessStatus.Failed,
                "Microphone access is blocked in Windows privacy settings.");
        }
        catch
        {
            return new ReadinessCheck(
                ReadinessStatus.Failed,
                "Microphone unavailable or in use.");
        }
    }

    private ReadinessCheck CheckNetwork()
    {
        var profiles = connectivity.ConnectionProfiles
            .Select(FormatProfile)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var profileDescription = profiles.Length == 0
            ? "network interface"
            : string.Join(" / ", profiles);

        return connectivity.NetworkAccess switch
        {
            NetworkAccess.Internet or NetworkAccess.Local =>
                new ReadinessCheck(
                    ReadinessStatus.Ready,
                    $"Connected through {profileDescription}."),
            NetworkAccess.ConstrainedInternet =>
                new ReadinessCheck(
                    ReadinessStatus.Warning,
                    $"Limited connection through {profileDescription}."),
            NetworkAccess.None =>
                new ReadinessCheck(
                    ReadinessStatus.Failed,
                    "No network connection detected."),
            _ =>
                new ReadinessCheck(
                    ReadinessStatus.Failed,
                    "Network status could not be determined.")
        };
    }

    private static string FormatProfile(ConnectionProfile profile)
    {
        return profile switch
        {
            ConnectionProfile.Ethernet => "Ethernet",
            ConnectionProfile.WiFi => "Wi-Fi",
            ConnectionProfile.Cellular => "cellular",
            ConnectionProfile.Bluetooth => "Bluetooth",
            _ => "network interface"
        };
    }
}
#endif
