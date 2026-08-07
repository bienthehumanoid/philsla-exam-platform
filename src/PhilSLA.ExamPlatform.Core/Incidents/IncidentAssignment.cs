namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentAssignment(
    Guid SessionId,
    string SessionTitle,
    string Room,
    Guid CandidateId,
    string StudentNumber,
    string CandidateName)
{
    private Guid _sessionId = RequireId(SessionId, nameof(SessionId));
    private string _sessionTitle = RequireText(SessionTitle, nameof(SessionTitle));
    private string _room = RequireText(Room, nameof(Room));
    private Guid _candidateId = RequireId(CandidateId, nameof(CandidateId));
    private string _studentNumber = RequireText(StudentNumber, nameof(StudentNumber));
    private string _candidateName = RequireText(CandidateName, nameof(CandidateName));

    public Guid SessionId { get => _sessionId; init => _sessionId = RequireId(value, nameof(SessionId)); }
    public string SessionTitle { get => _sessionTitle; init => _sessionTitle = RequireText(value, nameof(SessionTitle)); }
    public string Room { get => _room; init => _room = RequireText(value, nameof(Room)); }
    public Guid CandidateId { get => _candidateId; init => _candidateId = RequireId(value, nameof(CandidateId)); }
    public string StudentNumber { get => _studentNumber; init => _studentNumber = RequireText(value, nameof(StudentNumber)); }
    public string CandidateName { get => _candidateName; init => _candidateName = RequireText(value, nameof(CandidateName)); }

    private static Guid RequireId(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException("An ID is required.", name) : value;

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", name)
            : value.Trim();
}
