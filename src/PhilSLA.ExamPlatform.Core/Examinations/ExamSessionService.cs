namespace PhilSLA.ExamPlatform.Core.Examinations;

public sealed class ExamSessionService(
    IExamDefinitionProvider definitionProvider,
    IExamAttemptStore attemptStore,
    TimeProvider timeProvider)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ExamDefinition? _definition;
    private ExamAttemptRecord? _attempt;
    private Guid? _trackedBlockAttemptId;
    private long _trackedTimestamp;
    private TimeSpan _remainingAtTrack;

    public async Task<ExamSessionSnapshot> StartAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var definition = await GetDefinitionAsync(cancellationToken);
            var attempt = await attemptStore.LoadAsync(
                candidateId,
                definition.Id,
                cancellationToken);

            attempt ??= await attemptStore.CreateAsync(
                candidateId,
                definition,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot?> LoadAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var definition = await GetDefinitionAsync(cancellationToken);
            var attempt = await attemptStore.LoadAsync(
                candidateId,
                definition.Id,
                cancellationToken);

            if (attempt is null)
            {
                return null;
            }

            var snapshot = SetState(definition, attempt);
            return await SubmitIfExpiredAsync(snapshot, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ExamSessionSnapshot? GetCurrent()
    {
        return _definition is null || _attempt is null
            ? null
            : BuildSnapshot();
    }

    public async Task<ExamSessionSnapshot> SelectAnswerAsync(
        Guid questionId,
        Guid choiceId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireActiveSession();
            var question = snapshot.ActiveBlock.Questions.SingleOrDefault(
                item => item.Id == questionId)
                ?? throw new InvalidOperationException(
                    "The question does not belong to the active block.");

            if (question.Choices.All(choice => choice.Id != choiceId))
            {
                throw new InvalidOperationException(
                    "The selected choice does not belong to the question.");
            }

            var attempt = await attemptStore.AppendAnswerAsync(
                snapshot.Attempt.Id,
                snapshot.ActiveBlock.Id,
                questionId,
                choiceId,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(snapshot.Definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot> SetQuestionFlagAsync(
        Guid questionId,
        bool isFlagged,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireActiveSession();
            if (snapshot.ActiveBlock.Questions.All(
                question => question.Id != questionId))
            {
                throw new InvalidOperationException(
                    "The question does not belong to the active block.");
            }

            var attempt = await attemptStore.SetQuestionFlagAsync(
                snapshot.Attempt.Id,
                snapshot.ActiveBlock.Id,
                questionId,
                isFlagged,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(snapshot.Definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot> NavigateAsync(
        int questionNumber,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireActiveSession();
            if (snapshot.ActiveBlock.Questions.All(
                question => question.Number != questionNumber))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(questionNumber),
                    "The question number is outside the active block.");
            }

            var attempt = await attemptStore.SetCurrentQuestionAsync(
                snapshot.Attempt.Id,
                questionNumber,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(snapshot.Definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot> SubmitActiveBlockAsync(
        BlockSubmissionReason reason,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireActiveSession();
            var completesExam =
                snapshot.Attempt.ActiveBlockNumber ==
                snapshot.Definition.Blocks.Count;
            var attempt = await attemptStore.SubmitActiveBlockAsync(
                snapshot.Attempt.Id,
                reason,
                completesExam,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(snapshot.Definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot> StartNextBlockAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireSession();
            if (snapshot.Attempt.Status != ExamAttemptStatus.AwaitingNextBlock)
            {
                throw new InvalidOperationException(
                    "The next block cannot be started from the current state.");
            }

            var nextBlockNumber = snapshot.Attempt.ActiveBlockNumber + 1;
            var nextBlock = snapshot.Definition.Blocks.Single(
                block => block.Number == nextBlockNumber);
            var attempt = await attemptStore.StartNextBlockAsync(
                snapshot.Attempt.Id,
                nextBlock,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return SetState(snapshot.Definition, attempt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ExamSessionSnapshot> CheckTimeoutAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = RequireSession();
            return await SubmitIfExpiredAsync(snapshot, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ExamSessionSnapshot> SubmitIfExpiredAsync(
        ExamSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Attempt.Status != ExamAttemptStatus.Active ||
            snapshot.RemainingTime > TimeSpan.Zero)
        {
            return snapshot;
        }

        var completesExam =
            snapshot.Attempt.ActiveBlockNumber ==
            snapshot.Definition.Blocks.Count;
        var attempt = await attemptStore.SubmitActiveBlockAsync(
            snapshot.Attempt.Id,
            BlockSubmissionReason.TimeExpired,
            completesExam,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return SetState(snapshot.Definition, attempt);
    }

    private async Task<ExamDefinition> GetDefinitionAsync(
        CancellationToken cancellationToken)
    {
        return _definition ??=
            await definitionProvider.GetExamAsync(cancellationToken);
    }

    private ExamSessionSnapshot RequireSession()
    {
        return _definition is null || _attempt is null
            ? throw new InvalidOperationException(
                "No examination session is loaded.")
            : BuildSnapshot();
    }

    private ExamSessionSnapshot RequireActiveSession()
    {
        var snapshot = RequireSession();
        if (snapshot.Attempt.Status != ExamAttemptStatus.Active ||
            snapshot.RemainingTime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The active block is no longer accepting changes.");
        }

        return snapshot;
    }

    private ExamSessionSnapshot SetState(
        ExamDefinition definition,
        ExamAttemptRecord attempt)
    {
        _definition = definition;
        _attempt = attempt;
        TrackActiveBlock(attempt);
        return BuildSnapshot();
    }

    private ExamSessionSnapshot BuildSnapshot()
    {
        return new ExamSessionSnapshot(
            _definition!,
            _attempt!,
            GetRemainingTime(_attempt!));
    }

    private void TrackActiveBlock(ExamAttemptRecord attempt)
    {
        if (attempt.Status != ExamAttemptStatus.Active)
        {
            _trackedBlockAttemptId = null;
            return;
        }

        var activeBlock = attempt.Blocks.Single(
            block => block.BlockNumber == attempt.ActiveBlockNumber);
        if (_trackedBlockAttemptId == activeBlock.Id)
        {
            return;
        }

        _trackedBlockAttemptId = activeBlock.Id;
        _trackedTimestamp = timeProvider.GetTimestamp();
        _remainingAtTrack = Positive(
            activeBlock.DeadlineAtUtc - timeProvider.GetUtcNow());
    }

    private TimeSpan GetRemainingTime(ExamAttemptRecord attempt)
    {
        if (attempt.Status != ExamAttemptStatus.Active)
        {
            return TimeSpan.Zero;
        }

        var activeBlock = attempt.Blocks.Single(
            block => block.BlockNumber == attempt.ActiveBlockNumber);
        var wallClockRemaining = Positive(
            activeBlock.DeadlineAtUtc - timeProvider.GetUtcNow());
        var monotonicRemaining = Positive(
            _remainingAtTrack -
            timeProvider.GetElapsedTime(_trackedTimestamp));

        return wallClockRemaining <= monotonicRemaining
            ? wallClockRemaining
            : monotonicRemaining;
    }

    private static TimeSpan Positive(TimeSpan value)
    {
        return value > TimeSpan.Zero ? value : TimeSpan.Zero;
    }
}
