namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed class AttendancePolicyException(string message) : InvalidOperationException(message);
