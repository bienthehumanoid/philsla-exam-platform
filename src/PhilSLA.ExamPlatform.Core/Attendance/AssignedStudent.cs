namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed record AssignedStudent(
    Guid Id,
    string StudentNumber,
    string FullName,
    string ReferencePhotoPath);
