using PhilSLA.ExamPlatform.Core.Examinations;

namespace PhilSLA.ExamPlatform.Candidate.Examination;

public sealed class SeededExamDefinitionProvider : IExamDefinitionProvider
{
    private static readonly ExamDefinition Definition = new(
        Guid.Parse("10000000-0000-4000-8000-000000000001"),
        "PhilSLA 2026 Global Assessment",
        new[]
        {
            CreateBlock(1, "Mathematics"),
            CreateBlock(2, "Science"),
            CreateBlock(3, "English"),
            CreateBlock(4, "Abstract Reasoning")
        });

    public Task<ExamDefinition> GetExamAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Definition);
    }

    private static ExamBlockDefinition CreateBlock(
        int blockNumber,
        string title)
    {
        return new ExamBlockDefinition(
            CreateId(blockNumber),
            blockNumber,
            title,
            TimeSpan.FromMinutes(60),
            Enumerable
                .Range(1, 40)
                .Select(questionNumber => CreateQuestion(
                    blockNumber,
                    title,
                    questionNumber))
                .ToArray());
    }

    private static ExamQuestionDefinition CreateQuestion(
        int blockNumber,
        string blockTitle,
        int questionNumber)
    {
        var prompt = blockNumber == 1 && questionNumber == 1
            ? "[Mathematics] This is a sample question for assessment. " +
              "Which is the correct theoretical approach for this scenario?"
            : $"[{blockTitle}] Sample assessment question {questionNumber}. " +
              "Select the best response.";

        var choiceTexts = blockNumber == 1 && questionNumber == 1
            ? new[]
            {
                "Theorem Beta",
                "Hypothesis Alpha",
                "Protocol Gamma",
                "System Delta"
            }
            : new[]
            {
                $"Response A for question {questionNumber}",
                $"Response B for question {questionNumber}",
                $"Response C for question {questionNumber}",
                $"Response D for question {questionNumber}"
            };

        return new ExamQuestionDefinition(
            CreateId(blockNumber, questionNumber),
            questionNumber,
            prompt,
            choiceTexts
                .Select((text, index) => new ExamChoiceDefinition(
                    CreateId(blockNumber, questionNumber, index + 1),
                    ((char)('A' + index)).ToString(),
                    text))
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
