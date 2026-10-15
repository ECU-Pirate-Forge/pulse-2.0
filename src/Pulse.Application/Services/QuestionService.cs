using Pulse.Domain.Entities;

namespace Pulse.Application.Services;

public sealed record QuestionDTO
{
    public QuestionType Type { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
    public IReadOnlyList<MatchingPair> MatchingPairs { get; init; } = [];
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
        if (question.Type is QuestionType.LegacyLikertScale or QuestionType.LegacyOpenEnded)
        {
            return QuestionValidationResult.Failure(
                "This legacy question type can no longer be authored.");
        }

        if (question.Type is QuestionType.MultipleChoice or QuestionType.MultipleAnswer)
        {
            var validOptions = question.Options
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToList();

            if (validOptions.Count < 2)
            {
                return QuestionValidationResult.Failure(
                    question.Type == QuestionType.MultipleChoice
                        ? "Multiple-choice questions must include at least 2 options."
                        : "Multiple-answer questions must include at least 2 options.");
            }
        }

        if (question.Type == QuestionType.Matching)
        {
            var completePairs = question.MatchingPairs
                .Where(pair =>
                    !string.IsNullOrWhiteSpace(pair.Prompt) &&
                    !string.IsNullOrWhiteSpace(pair.Match))
                .ToList();

            if (completePairs.Count < 2 || completePairs.Count != question.MatchingPairs.Count)
            {
                return QuestionValidationResult.Failure(
                    "Matching questions must include at least 2 complete prompt-and-match pairs.");
            }

            if (completePairs.Select(pair => pair.Prompt.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != completePairs.Count)
            {
                return QuestionValidationResult.Failure(
                    "Matching question prompts must be unique.");
            }
        }

        if (question.Type is not (
            QuestionType.MultipleChoice or
            QuestionType.MultipleAnswer or
            QuestionType.TrueFalse or
            QuestionType.Matching))
        {
            return QuestionValidationResult.Failure("Question type is invalid.");
        }

        return QuestionValidationResult.Success();
    }
}
