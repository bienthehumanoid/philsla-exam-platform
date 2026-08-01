namespace PhilSLA.ExamPlatform.Core.Examinations;

public sealed record ExamDefinition(
    Guid Id,
    string Title,
    IReadOnlyList<ExamBlockDefinition> Blocks);

public sealed record ExamBlockDefinition(
    Guid Id,
    int Number,
    string Title,
    TimeSpan Duration,
    IReadOnlyList<ExamQuestionDefinition> Questions);

public sealed record ExamQuestionDefinition(
    Guid Id,
    int Number,
    string Prompt,
    IReadOnlyList<ExamChoiceDefinition> Choices);

public sealed record ExamChoiceDefinition(
    Guid Id,
    string Label,
    string Text);
