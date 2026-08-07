namespace PhilSLA.ExamPlatform.Candidate.Devices;

public sealed class DeviceIdentityUnavailableException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
