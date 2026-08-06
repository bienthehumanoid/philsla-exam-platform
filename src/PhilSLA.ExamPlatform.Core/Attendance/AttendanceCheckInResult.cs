namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AttendanceCheckInResult(
    AttendanceSessionSnapshot Snapshot,
    bool WasCreated);
