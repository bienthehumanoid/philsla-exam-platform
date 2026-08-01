namespace PhilSLA.ExamPlatform.Core.Examinations;

public sealed record ExamSessionSnapshot(
    ExamDefinition Definition,
    ExamAttemptRecord Attempt,
    TimeSpan RemainingTime)
{
    public ExamBlockDefinition ActiveBlock =>
        Definition.Blocks.Single(
            block => block.Number == Attempt.ActiveBlockNumber);

    public BlockAttemptRecord ActiveBlockAttempt =>
        Attempt.Blocks.Single(
            block => block.BlockNumber == Attempt.ActiveBlockNumber);

    public ExamQuestionDefinition CurrentQuestion =>
        ActiveBlock.Questions.Single(
            question => question.Number == Attempt.CurrentQuestionNumber);

    public int AnsweredCount =>
        ActiveBlock.Questions.Count(
            question => Attempt.Answers.ContainsKey(question.Id));

    public int FlaggedCount =>
        ActiveBlock.Questions.Count(
            question => Attempt.FlaggedQuestionIds.Contains(question.Id));
}
