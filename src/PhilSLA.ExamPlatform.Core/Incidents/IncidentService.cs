namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed class IncidentService
{
    public const int MaximumAttachmentCount = 5;

    private readonly IIncidentCategoryProvider _categoryProvider;
    private readonly IIncidentAssignmentProvider _assignmentProvider;
    private readonly IIncidentStore _store;
    private readonly TimeProvider _timeProvider;

    public IncidentService(
        IIncidentCategoryProvider categoryProvider,
        IIncidentAssignmentProvider assignmentProvider,
        IIncidentStore store,
        TimeProvider timeProvider)
    {
        _categoryProvider = categoryProvider ?? throw new ArgumentNullException(nameof(categoryProvider));
        _assignmentProvider = assignmentProvider ?? throw new ArgumentNullException(nameof(assignmentProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<IncidentRecord>> LoadAssignedAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await GetAssignmentsAsync(proctorId, cancellationToken);
        var sessionIds = assignments
            .Select(assignment => assignment.SessionId)
            .Distinct()
            .ToArray();
        return sessionIds.Length == 0
            ? []
            : await _store.LoadForSessionsAsync(sessionIds, cancellationToken);
    }

    public async Task<IncidentCreationOptions> LoadCreationOptionsAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await GetAssignmentsAsync(proctorId, cancellationToken);
        var categories = await _categoryProvider.GetAsync(cancellationToken);
        return new IncidentCreationOptions(
            assignments
                .OrderBy(assignment => assignment.CandidateName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(assignment => assignment.SessionTitle, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            categories
                .Where(category => category.IsActive)
                .OrderBy(category => category.DisplayOrder)
                .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public async Task<IncidentRecord> CreateAsync(
        IncidentCreateCommand command,
        Guid proctorId,
        string proctorName,
        IReadOnlyList<IncidentEvidenceUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(uploads);
        if (proctorId == Guid.Empty || string.IsNullOrWhiteSpace(proctorName))
        {
            throw new IncidentValidationException("An authenticated proctor is required.");
        }

        if (uploads.Count > MaximumAttachmentCount)
        {
            throw new IncidentValidationException(
                $"No more than {MaximumAttachmentCount} evidence images may be attached.");
        }

        if (!Enum.IsDefined(command.Severity))
        {
            throw new IncidentValidationException("Select a valid severity level.");
        }

        var description = RequireDescription(command.Description);
        var assignments = await GetAssignmentsAsync(proctorId, cancellationToken);
        var assignment = assignments.SingleOrDefault(item =>
            item.SessionId == command.SessionId && item.CandidateId == command.CandidateId)
            ?? throw new IncidentValidationException(
                "The candidate is not assigned to this proctor and examination session.");
        var categories = await _categoryProvider.GetAsync(cancellationToken);
        var category = categories.SingleOrDefault(item =>
            item.Id == command.CategoryId && item.IsActive)
            ?? throw new IncidentValidationException("Select an active incident category.");

        var draft = new IncidentRecord(
            Guid.NewGuid(),
            string.Empty,
            assignment.SessionId,
            assignment.SessionTitle,
            assignment.Room,
            assignment.CandidateId,
            assignment.StudentNumber,
            assignment.CandidateName,
            category.Id,
            category.Name,
            command.Severity,
            description,
            IncidentReviewStatus.Pending,
            proctorId,
            proctorName.Trim(),
            _timeProvider.GetUtcNow().ToUniversalTime(),
            []);
        return await _store.CreateAsync(draft, uploads, cancellationToken);
    }

    public async Task<byte[]> ReadEvidenceAsync(
        Guid proctorId,
        Guid incidentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadAssignedAsync(proctorId, cancellationToken);
        var record = records.SingleOrDefault(item => item.Id == incidentId)
            ?? throw new IncidentValidationException("The incident is not assigned to this proctor.");
        if (!record.Attachments.Any(item => item.Id == attachmentId))
        {
            throw new IncidentValidationException("The evidence attachment does not belong to this incident.");
        }

        return await _store.ReadEvidenceAsync(incidentId, attachmentId, cancellationToken);
    }

    private async Task<IReadOnlyList<IncidentAssignment>> GetAssignmentsAsync(
        Guid proctorId,
        CancellationToken cancellationToken)
    {
        if (proctorId == Guid.Empty)
        {
            throw new IncidentValidationException("An authenticated proctor is required.");
        }

        return await _assignmentProvider.GetAssignedAsync(proctorId, cancellationToken);
    }

    private static string RequireDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IncidentValidationException("Describe the incident before submitting it.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= IncidentRecord.MaximumDescriptionLength
            ? trimmed
            : throw new IncidentValidationException(
                $"Description cannot exceed {IncidentRecord.MaximumDescriptionLength} characters.");
    }
}
