using PhilSLA.ExamPlatform.Core.Examinations;

namespace PhilSLA.ExamPlatform.Candidate.Tests;

internal static class ExamTestData
{
    public static readonly ExamDefinition Definition = new(
        Guid.Parse("10000000-0000-4000-8000-000000000001"),
        "Test Assessment",
        new[]
        {
            CreateBlock(1, "Mathematics"),
            CreateBlock(2, "Science")
        });

    private static ExamBlockDefinition CreateBlock(int number, string title)
    {
        return new ExamBlockDefinition(
            CreateId(number),
            number,
            title,
            TimeSpan.FromMinutes(60),
            Enumerable
                .Range(1, 3)
                .Select(questionNumber => new ExamQuestionDefinition(
                    CreateId(number, questionNumber),
                    questionNumber,
                    $"Question {questionNumber} for {title}",
                    new[]
                    {
                        new ExamChoiceDefinition(
                            CreateId(number, questionNumber, 1),
                            "A",
                            "Answer A"),
                        new ExamChoiceDefinition(
                            CreateId(number, questionNumber, 2),
                            "B",
                            "Answer B")
                    }))
                .ToArray());
    }

    private static Guid CreateId(
        int blockNumber,
        int questionNumber = 0,
        int choiceNumber = 0)
    {
        return Guid.Parse(
            $"{blockNumber:D2}{questionNumber:D6}-" +
            $"{choiceNumber:D4}-4000-8000-000000000001");
    }
}

internal sealed class TestExamDefinitionProvider(
    ExamDefinition? definition = null)
    : IExamDefinitionProvider
{
    public Task<ExamDefinition> GetExamAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(definition ?? ExamTestData.Definition);
    }
}

internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public override long GetTimestamp()
    {
        return _timestamp;
    }

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
        _timestamp += duration.Ticks;
    }
}

internal sealed class InMemoryExamAttemptStore : IExamAttemptStore
{
    private ExamAttemptRecord? _attempt;

    public int AnswerRevisionCount { get; private set; }

    public Task<ExamAttemptRecord?> LoadAsync(
        Guid candidateId,
        Guid examId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _attempt is
            {
                CandidateId: var storedCandidateId,
                ExamId: var storedExamId
            } &&
            storedCandidateId == candidateId &&
            storedExamId == examId
                ? _attempt
                : null);
    }

    public Task<ExamAttemptRecord> CreateAsync(
        Guid candidateId,
        ExamDefinition definition,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (_attempt is not null)
        {
            return Task.FromResult(_attempt);
        }

        var firstBlock = definition.Blocks.Single(block => block.Number == 1);
        _attempt = new ExamAttemptRecord(
            Guid.NewGuid(),
            candidateId,
            definition.Id,
            ExamAttemptStatus.Active,
            1,
            1,
            startedAtUtc,
            startedAtUtc,
            new[]
            {
                new BlockAttemptRecord(
                    Guid.NewGuid(),
                    firstBlock.Id,
                    1,
                    BlockAttemptStatus.Active,
                    startedAtUtc,
                    startedAtUtc.Add(firstBlock.Duration),
                    null,
                    null)
            },
            new Dictionary<Guid, AnswerSelectionRecord>(),
            new HashSet<Guid>());
        return Task.FromResult(_attempt);
    }

    public Task<ExamAttemptRecord> AppendAnswerAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        Guid choiceId,
        DateTimeOffset savedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var attempt = RequireAttempt(attemptId);
        var answers = attempt.Answers.ToDictionary();
        var revision = answers.TryGetValue(questionId, out var previous)
            ? previous.RevisionNumber + 1
            : 1;
        answers[questionId] = new AnswerSelectionRecord(
            questionId,
            choiceId,
            revision,
            savedAtUtc,
            $"TEST-HASH-{revision}");
        AnswerRevisionCount++;
        _attempt = attempt with
        {
            Answers = answers,
            UpdatedAtUtc = savedAtUtc
        };
        return Task.FromResult(_attempt);
    }

    public Task<ExamAttemptRecord> SetQuestionFlagAsync(
        Guid attemptId,
        Guid blockId,
        Guid questionId,
        bool isFlagged,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var attempt = RequireAttempt(attemptId);
        var flags = attempt.FlaggedQuestionIds.ToHashSet();
        if (isFlagged)
        {
            flags.Add(questionId);
        }
        else
        {
            flags.Remove(questionId);
        }

        _attempt = attempt with
        {
            FlaggedQuestionIds = flags,
            UpdatedAtUtc = updatedAtUtc
        };
        return Task.FromResult(_attempt);
    }

    public Task<ExamAttemptRecord> SetCurrentQuestionAsync(
        Guid attemptId,
        int questionNumber,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _attempt = RequireAttempt(attemptId) with
        {
            CurrentQuestionNumber = questionNumber,
            UpdatedAtUtc = updatedAtUtc
        };
        return Task.FromResult(_attempt);
    }

    public Task<ExamAttemptRecord> SubmitActiveBlockAsync(
        Guid attemptId,
        BlockSubmissionReason reason,
        bool completesExam,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var attempt = RequireAttempt(attemptId);
        var blocks = attempt.Blocks
            .Select(block =>
                block.Status == BlockAttemptStatus.Active
                    ? block with
                    {
                        Status = reason == BlockSubmissionReason.TimeExpired
                            ? BlockAttemptStatus.TimedOut
                            : BlockAttemptStatus.Submitted,
                        SubmittedAtUtc = submittedAtUtc,
                        SubmissionReason = reason
                    }
                    : block)
            .ToArray();
        _attempt = attempt with
        {
            Status = completesExam
                ? ExamAttemptStatus.Completed
                : ExamAttemptStatus.AwaitingNextBlock,
            Blocks = blocks,
            UpdatedAtUtc = submittedAtUtc
        };
        return Task.FromResult(_attempt);
    }

    public Task<ExamAttemptRecord> StartNextBlockAsync(
        Guid attemptId,
        ExamBlockDefinition block,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var attempt = RequireAttempt(attemptId);
        var blocks = attempt.Blocks
            .Append(new BlockAttemptRecord(
                Guid.NewGuid(),
                block.Id,
                block.Number,
                BlockAttemptStatus.Active,
                startedAtUtc,
                startedAtUtc.Add(block.Duration),
                null,
                null))
            .ToArray();
        _attempt = attempt with
        {
            Status = ExamAttemptStatus.Active,
            ActiveBlockNumber = block.Number,
            CurrentQuestionNumber = 1,
            Blocks = blocks,
            UpdatedAtUtc = startedAtUtc
        };
        return Task.FromResult(_attempt);
    }

    private ExamAttemptRecord RequireAttempt(Guid attemptId)
    {
        return _attempt is { Id: var id } && id == attemptId
            ? _attempt
            : throw new InvalidOperationException("Attempt not found.");
    }
}
