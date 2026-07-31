namespace PhilSLA.ExamPlatform.Candidate.Examination;

public sealed record ExamAssignment(
    string Title,
    DateOnly Date,
    string Center,
    string Room,
    string Proctor);
