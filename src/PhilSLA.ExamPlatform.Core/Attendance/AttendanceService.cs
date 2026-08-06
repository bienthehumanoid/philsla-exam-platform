namespace PhilSLA.ExamPlatform.Core.Attendance;

public sealed class AttendanceService
{
    private readonly IAttendanceSessionProvider _sessionProvider;
    private readonly IAttendanceStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public AttendanceService(
        IAttendanceSessionProvider sessionProvider,
        IAttendanceStore store,
        TimeProvider timeProvider)
    {
        _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AttendanceSessionSnapshot> LoadAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var definition = await GetAssignedSessionAsync(sessionId, proctorId, cancellationToken);
        var record = await LoadOrCreateAsync(definition, cancellationToken);
        return new AttendanceSessionSnapshot(definition, record);
    }

    public async Task<IReadOnlyList<AttendanceSessionSnapshot>> LoadAssignedAsync(
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var definitions = await _sessionProvider.GetAssignedSessionsAsync(proctorId, cancellationToken);
        var snapshots = new List<AttendanceSessionSnapshot>(definitions.Count);
        foreach (var definition in definitions)
        {
            var record = await LoadOrCreateAsync(definition, cancellationToken);
            snapshots.Add(new AttendanceSessionSnapshot(definition, record));
        }

        return snapshots.AsReadOnly();
    }

    public async Task<AttendanceSessionSnapshot> CheckInAsync(
        Guid sessionId,
        Guid studentId,
        AttendanceCheckInMethod method,
        Guid proctorId,
        string? credentialId,
        string? manualReason,
        CancellationToken cancellationToken = default) =>
        (await CheckInWithResultAsync(
            sessionId,
            studentId,
            method,
            proctorId,
            credentialId,
            manualReason,
            cancellationToken)).Snapshot;

    public async Task<AttendanceCheckInResult> CheckInWithResultAsync(
        Guid sessionId,
        Guid studentId,
        AttendanceCheckInMethod method,
        Guid proctorId,
        string? credentialId,
        string? manualReason,
        CancellationToken cancellationToken = default)
    {
        if (method is not AttendanceCheckInMethod.Qr and not AttendanceCheckInMethod.Manual)
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported check-in method.");
        }

        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var (definition, record) = await ReloadForMutationAsync(
                sessionId,
                proctorId,
                cancellationToken);
            EnsureWritable(record);
            var entry = GetAssignedEntry(definition, record, studentId);

            if (IsAdmissible(entry.Status))
            {
                return new AttendanceCheckInResult(
                    new AttendanceSessionSnapshot(definition, record),
                    WasCreated: false);
            }

            var receivedAtUtc = _timeProvider.GetUtcNow();
            var decision = definition.Policy.Classify(definition.StartsAtUtc, receivedAtUtc);
            var status = decision switch
            {
                AttendanceCheckInDecision.Present => AttendanceStatus.Present,
                AttendanceCheckInDecision.Late => AttendanceStatus.Late,
                AttendanceCheckInDecision.NotOpen => throw new InvalidOperationException("Check-in has not opened."),
                AttendanceCheckInDecision.Closed => throw new InvalidOperationException("Check-in closed."),
                _ => throw new InvalidOperationException("Unsupported check-in decision.")
            };

            var normalizedCredentialId = method == AttendanceCheckInMethod.Qr
                ? RequireText(credentialId, nameof(credentialId))
                : null;
            var normalizedManualReason = method == AttendanceCheckInMethod.Manual
                ? RequireText(manualReason, nameof(manualReason))
                : null;
            var changedEntry = new AttendanceEntry(
                entry.StudentId,
                status,
                method,
                receivedAtUtc,
                normalizedCredentialId,
                normalizedManualReason,
                method == AttendanceCheckInMethod.Manual ? proctorId : null);
            var auditReason = normalizedManualReason ?? "QR check-in.";
            var changed = ReplaceEntry(
                record,
                changedEntry,
                new AttendanceAuditEntry(
                    Guid.NewGuid(),
                    studentId,
                    entry.Status,
                    status,
                    auditReason,
                    proctorId,
                    receivedAtUtc));
            var saved = await _store.SaveAsync(changed, record.Version, cancellationToken);
            return new AttendanceCheckInResult(
                new AttendanceSessionSnapshot(definition, saved),
                WasCreated: true);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<AttendanceSessionSnapshot> ApplyCutoffAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var (definition, record) = await ReloadForMutationAsync(
                sessionId,
                proctorId,
                cancellationToken);
            EnsureWritable(record);
            var occurredAtUtc = _timeProvider.GetUtcNow();
            var cutoffAtUtc = definition.StartsAtUtc + definition.Policy.LateGracePeriod;
            if (occurredAtUtc < cutoffAtUtc ||
                record.Entries.All(entry => entry.Status != AttendanceStatus.Unmarked))
            {
                return new AttendanceSessionSnapshot(definition, record);
            }

            var entries = record.Entries
                .Select(entry => entry.Status == AttendanceStatus.Unmarked
                    ? new AttendanceEntry(
                        entry.StudentId,
                        AttendanceStatus.PendingAbsence,
                        entry.CheckInMethod,
                        entry.ReceivedAtUtc,
                        entry.CredentialId,
                        entry.ManualReason,
                        entry.ConfirmedByProctorId)
                    : entry)
                .ToArray();
            var auditEntries = record.AuditEntries
                .Concat(record.Entries
                    .Where(entry => entry.Status == AttendanceStatus.Unmarked)
                    .Select(entry => new AttendanceAuditEntry(
                        Guid.NewGuid(),
                        entry.StudentId,
                        AttendanceStatus.Unmarked,
                        AttendanceStatus.PendingAbsence,
                        "Check-in cutoff reached.",
                        proctorId,
                        occurredAtUtc)))
                .ToArray();
            var changed = new AttendanceSessionRecord(
                record.SessionId,
                entries,
                auditEntries,
                record.FinalizedAtUtc,
                record.Version + 1);
            var saved = await _store.SaveAsync(changed, record.Version, cancellationToken);
            return new AttendanceSessionSnapshot(definition, saved);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<AttendanceSessionSnapshot> ConfirmAbsentAsync(
        Guid sessionId,
        Guid studentId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var (definition, record) = await ReloadForMutationAsync(
                sessionId,
                proctorId,
                cancellationToken);
            EnsureWritable(record);
            var entry = GetAssignedEntry(definition, record, studentId);
            if (entry.Status == AttendanceStatus.Absent)
            {
                return new AttendanceSessionSnapshot(definition, record);
            }

            if (entry.Status != AttendanceStatus.PendingAbsence)
            {
                throw new InvalidOperationException("Only a pending absence can be confirmed.");
            }

            var occurredAtUtc = _timeProvider.GetUtcNow();
            var changedEntry = new AttendanceEntry(
                entry.StudentId,
                AttendanceStatus.Absent,
                entry.CheckInMethod,
                entry.ReceivedAtUtc,
                entry.CredentialId,
                entry.ManualReason,
                proctorId);
            var changed = ReplaceEntry(
                record,
                changedEntry,
                new AttendanceAuditEntry(
                    Guid.NewGuid(),
                    studentId,
                    entry.Status,
                    AttendanceStatus.Absent,
                    "Absence confirmed.",
                    proctorId,
                    occurredAtUtc));
            var saved = await _store.SaveAsync(changed, record.Version, cancellationToken);
            return new AttendanceSessionSnapshot(definition, saved);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<AttendanceSessionSnapshot> CorrectAsync(
        Guid sessionId,
        Guid studentId,
        AttendanceStatus replacement,
        string reason,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = RequireText(reason, nameof(reason));
        if (replacement is not AttendanceStatus.Present and
            not AttendanceStatus.Late and
            not AttendanceStatus.Absent)
        {
            throw new ArgumentOutOfRangeException(nameof(replacement));
        }

        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var (definition, record) = await ReloadForMutationAsync(
                sessionId,
                proctorId,
                cancellationToken);
            EnsureWritable(record);
            var entry = GetAssignedEntry(definition, record, studentId);
            var occurredAtUtc = _timeProvider.GetUtcNow();
            var cutoffAtUtc = definition.StartsAtUtc + definition.Policy.LateGracePeriod;
            var createsAdmission = IsAdmissible(replacement) && !IsAdmissible(entry.Status);
            var hasPreCutoffReceipt = entry.ReceivedAtUtc.HasValue && entry.ReceivedAtUtc < cutoffAtUtc;
            if (occurredAtUtc >= cutoffAtUtc && createsAdmission && !hasPreCutoffReceipt)
            {
                throw new InvalidOperationException(
                    "Post-cutoff admission requires pre-cutoff check-in evidence.");
            }

            var changedEntry = new AttendanceEntry(
                entry.StudentId,
                replacement,
                entry.CheckInMethod,
                entry.ReceivedAtUtc,
                entry.CredentialId,
                entry.ManualReason,
                proctorId);
            var changed = ReplaceEntry(
                record,
                changedEntry,
                new AttendanceAuditEntry(
                    Guid.NewGuid(),
                    studentId,
                    entry.Status,
                    replacement,
                    normalizedReason,
                    proctorId,
                    occurredAtUtc));
            var saved = await _store.SaveAsync(changed, record.Version, cancellationToken);
            return new AttendanceSessionSnapshot(definition, saved);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<AttendanceSessionSnapshot> FinalizeAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var (definition, record) = await ReloadForMutationAsync(
                sessionId,
                proctorId,
                cancellationToken);
            EnsureWritable(record);
            var finalizedAtUtc = _timeProvider.GetUtcNow();
            if (finalizedAtUtc < definition.EndsAtUtc)
            {
                throw new InvalidOperationException("Attendance cannot be finalized before the scheduled end.");
            }

            if (record.Entries.Any(entry =>
                entry.Status is AttendanceStatus.Unmarked or AttendanceStatus.PendingAbsence))
            {
                throw new InvalidOperationException("Confirm all pending absences before finalizing attendance.");
            }

            var changed = new AttendanceSessionRecord(
                record.SessionId,
                record.Entries,
                record.AuditEntries,
                finalizedAtUtc,
                record.Version + 1);
            var saved = await _store.SaveAsync(changed, record.Version, cancellationToken);
            return new AttendanceSessionSnapshot(definition, saved);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<bool> CanAdmitAsync(
        Guid sessionId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var record = await _store.LoadAsync(sessionId, cancellationToken);
        return record?.Entries.Any(entry =>
            entry.StudentId == studentId && IsAdmissible(entry.Status)) == true;
    }

    private async Task<AttendanceSessionDefinition> GetAssignedSessionAsync(
        Guid sessionId,
        Guid proctorId,
        CancellationToken cancellationToken) =>
        await _sessionProvider.GetSessionAsync(sessionId, proctorId, cancellationToken)
            ?? throw new InvalidOperationException("The attendance session is not assigned to this proctor.");

    private async Task<AttendanceSessionRecord> LoadOrCreateAsync(
        AttendanceSessionDefinition definition,
        CancellationToken cancellationToken) =>
        await _store.LoadAsync(definition.Id, cancellationToken)
            ?? await _store.CreateAsync(definition, cancellationToken);

    private async Task<(AttendanceSessionDefinition Definition, AttendanceSessionRecord Record)>
        ReloadForMutationAsync(
            Guid sessionId,
            Guid proctorId,
            CancellationToken cancellationToken)
    {
        var definition = await GetAssignedSessionAsync(sessionId, proctorId, cancellationToken);
        var record = await LoadOrCreateAsync(definition, cancellationToken);
        return (definition, record);
    }

    private static AttendanceEntry GetAssignedEntry(
        AttendanceSessionDefinition definition,
        AttendanceSessionRecord record,
        Guid studentId)
    {
        if (!definition.Students.Any(student => student.Id == studentId))
        {
            throw new InvalidOperationException("The student is not assigned to this session.");
        }

        return record.Entries.Single(entry => entry.StudentId == studentId);
    }

    private static AttendanceSessionRecord ReplaceEntry(
        AttendanceSessionRecord record,
        AttendanceEntry replacement,
        AttendanceAuditEntry auditEntry) =>
        new(
            record.SessionId,
            record.Entries
                .Select(entry => entry.StudentId == replacement.StudentId ? replacement : entry)
                .ToArray(),
            record.AuditEntries.Append(auditEntry).ToArray(),
            record.FinalizedAtUtc,
            record.Version + 1);

    private static string RequireText(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-blank value is required.", parameterName)
            : value.Trim();

    private static bool IsAdmissible(AttendanceStatus status) =>
        status is AttendanceStatus.Present or AttendanceStatus.Late;

    private static void EnsureWritable(AttendanceSessionRecord record)
    {
        if (record.FinalizedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Finalized attendance is read-only.");
        }
    }
}
