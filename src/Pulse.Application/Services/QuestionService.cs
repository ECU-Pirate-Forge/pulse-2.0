using Pulse.Domain.Entities;

namespace Pulse.Application.Services;

public sealed record QuestionDTO
{
    public QuestionType Type { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
}

public sealed record QuestionValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static QuestionValidationResult Success() =>
        new() { IsValid = true };

    public static QuestionValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

public class QuestionService
{
    public QuestionValidationResult ValidateQuestion(QuestionDTO question)
    {
        if (question.Type == QuestionType.MultipleChoice)
        {
            var validOptions = question.Options
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToList();

            if (validOptions.Count < 2)
            {
                return QuestionValidationResult.Failure(
                    "Multiple-choice questions must include at least 2 options.");
            }
        }

        return QuestionValidationResult.Success();
    }
}
