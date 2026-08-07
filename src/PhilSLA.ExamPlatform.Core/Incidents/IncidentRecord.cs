using System.Collections.ObjectModel;

namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentRecord(
    Guid Id,
    string DisplayId,
    Guid SessionId,
    string SessionTitle,
    string Room,
    Guid CandidateId,
    string StudentNumber,
    string CandidateName,
    Guid CategoryId,
    string CategoryName,
    IncidentSeverity Severity,
    string Description,
    IncidentReviewStatus ReviewStatus,
    Guid ReportedByProctorId,
    string ReportedByProctorName,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<IncidentAttachment> Attachments)
{
    public const int MaximumDescriptionLength = 4000;

    private string _description = RequireDescription(Description);
    private DateTimeOffset _createdAtUtc = RequireUtc(CreatedAtUtc);
    private IReadOnlyList<IncidentAttachment> _attachments = ToReadOnly(Attachments);

    public string Description
    {
        get => _description;
        init => _description = RequireDescription(value);
    }

    public DateTimeOffset CreatedAtUtc
    {
        get => _createdAtUtc;
        init => _createdAtUtc = RequireUtc(value);
    }

    public IReadOnlyList<IncidentAttachment> Attachments
    {
        get => _attachments;
        init => _attachments = ToReadOnly(value);
    }

    private static string RequireDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A description is required.", nameof(Description));
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaximumDescriptionLength
            ? trimmed
            : throw new ArgumentException(
                $"Description cannot exceed {MaximumDescriptionLength} characters.",
                nameof(Description));
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("The creation timestamp must use UTC.", nameof(CreatedAtUtc));

    private static IReadOnlyList<IncidentAttachment> ToReadOnly(IReadOnlyList<IncidentAttachment> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<IncidentAttachment>(values.ToArray());
    }
}
