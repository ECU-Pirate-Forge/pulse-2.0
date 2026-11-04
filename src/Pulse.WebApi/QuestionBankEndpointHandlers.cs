using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class QuestionBankEndpointHandlers
{
    public static Results<Created<QuestionBankItem>, BadRequest<string>> CreateQuestionBankItem(
        CreateQuestionBankItemRequest request,
        IQuestionBankRepository repo,
        QuestionService questionService)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return TypedResults.BadRequest("Text is required.");

        if (request.Type is null)
            return TypedResults.BadRequest("Type is required.");
        if (request.IsDraft && request.Type is not (QuestionType.MultipleChoice or QuestionType.MultipleAnswer or QuestionType.TrueFalse or QuestionType.Matching))
            return TypedResults.BadRequest("Question type is invalid.");

        var normalizedOptions = (request.Options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .ToList();

        var normalizedPairs = (request.MatchingPairs ?? [])
            .Select(pair => new MatchingPair
            {
                Prompt = pair?.Prompt?.Trim() ?? string.Empty,
                Match = pair?.Match?.Trim() ?? string.Empty
            })
            .ToList();

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = request.Type.Value,
            Options = normalizedOptions,
            MatchingPairs = normalizedPairs
        });
        if (!request.IsDraft && !validation.IsValid)
            return TypedResults.BadRequest(validation.ErrorMessage!);
        var triviaValidation = ValidateTriviaConfiguration(
            request.Type.Value,
            request.TriviaEligible,
            normalizedOptions,
            NormalizeOptions(request.CorrectAnswers));
        if (!request.IsDraft && triviaValidation is not null)
            return TypedResults.BadRequest(triviaValidation);

        var item = new QuestionBankItem
        {
            Text = request.Text.Trim(),
            Type = request.Type.Value,
            Options = normalizedOptions,
            MatchingPairs = normalizedPairs,
            Course = request.Course?.Trim() ?? string.Empty,
            Topic = request.Topic?.Trim() ?? string.Empty,
            Tags = NormalizeTags(request.Tags),
            IsDraft = request.IsDraft,
            ModerationStatus = QuestionModerationStatus.Approved,
            Source = QuestionSource.InstructorAuthored,
            ContributorAttribution = "Instructor",
            TriviaEligible = request.TriviaEligible,
            CorrectAnswers = NormalizeOptions(request.CorrectAnswers),
            Explanation = request.Explanation?.Trim() ?? string.Empty,
            SourceId = request.SourceId ?? string.Empty,
            SourceQuote = request.SourceQuote ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = repo.Insert(item);
        return TypedResults.Created($"/api/questionbank/{created.Id}", created);
    }

    public static Results<Ok<QuestionBankListResponse>, BadRequest<string>> GetQuestionBankItems(
        IQuestionBankRepository repo,
        string? text = null,
        int? type = null,
        string? course = null,
        string? topic = null,
        string? tag = null,
        QuestionModerationStatus? moderationStatus = null,
        QuestionSource? source = null,
        bool? triviaEligible = null,
        QuestionBankSort sort = QuestionBankSort.Newest,
        int page = 1,
        int pageSize = 20,
        bool? isDraft = null,
        Guid? importBatchId = null,
        string? correctAnswer = null,
        int? correctAnswerPosition = null,
        string? option = null)
    {
        if (page < 1)
            return TypedResults.BadRequest("Page must be greater than 0.");

        if (pageSize < 1)
            return TypedResults.BadRequest("PageSize must be greater than 0.");

        var query = new QuestionBankQuery(
            text,
            type,
            course,
            topic,
            tag,
            moderationStatus,
            source,
            triviaEligible,
            sort,
            isDraft,
            importBatchId,
            correctAnswer,
            correctAnswerPosition,
            option);
        if (query.Validate() is { } error)
            return TypedResults.BadRequest(error);

        var filtered = repo.Search(query).ToList();
        var totalCount = filtered.Count;
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return TypedResults.Ok(new QuestionBankListResponse(items, totalCount));
    }

    public static Results<Accepted<QuestionBankItem>, BadRequest<string>> SubmitQuestionSuggestion(
        SubmitQuestionSuggestionRequest request,
        IQuestionBankRepository repo,
        QuestionService questionService)
    {
        var validationError = ValidateRequest(request.Text, request.Type, request.Options, request.MatchingPairs, questionService);
        if (validationError is not null)
            return TypedResults.BadRequest(validationError);

        var item = new QuestionBankItem
        {
            Text = request.Text!.Trim(),
            Type = request.Type!.Value,
            Options = NormalizeOptions(request.Options),
            MatchingPairs = NormalizePairs(request.MatchingPairs),
            Course = request.Course?.Trim() ?? string.Empty,
            Topic = request.Topic?.Trim() ?? string.Empty,
            Tags = NormalizeTags(request.Tags),
            ModerationStatus = QuestionModerationStatus.Pending,
            Source = QuestionSource.StudentSuggestion,
            ContributorAttribution = "Anonymous student",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return TypedResults.Accepted($"/api/questionbank/{item.Id}", repo.Insert(item));
    }

    public static Results<Ok<QuestionBankItem>, NotFound, BadRequest<string>> ModerateQuestionBankItem(
        Guid id,
        ModerateQuestionBankItemRequest request,
        IQuestionBankRepository repo,
        QuestionService questionService)
    {
        var item = repo.GetById(id);
        if (item is null)
            return TypedResults.NotFound();

        if (request.ModerationStatus is not
            (QuestionModerationStatus.Approved or QuestionModerationStatus.Rejected))
        {
            return TypedResults.BadRequest("Moderation status must be Approved or Rejected.");
        }

        var text = request.Text?.Trim() ?? item.Text;
        var type = request.Type ?? item.Type;
        if ((request.IsDraft ?? item.IsDraft) && type is not (QuestionType.MultipleChoice or QuestionType.MultipleAnswer or QuestionType.TrueFalse or QuestionType.Matching))
            return TypedResults.BadRequest("Question type is invalid.");
        var options = request.Options is null ? item.Options : NormalizeOptions(request.Options);
        var matchingPairs = request.MatchingPairs is null
            ? item.MatchingPairs
            : NormalizePairs(request.MatchingPairs);
        var triviaEligible = request.TriviaEligible ?? item.TriviaEligible;
        var isDraft = request.IsDraft ?? item.IsDraft;
        if (isDraft && item.Source == QuestionSource.StudentSuggestion)
            return TypedResults.BadRequest("Student suggestions must be moderated, not saved as instructor drafts.");
        var correctAnswers = request.CorrectAnswers is null
            ? item.CorrectAnswers
            : NormalizeOptions(request.CorrectAnswers);
        var validationError = ValidateRequest(text, type, options, matchingPairs, questionService);
        if (string.IsNullOrWhiteSpace(text))
            return TypedResults.BadRequest("Text is required.");
        if (!isDraft && validationError is not null)
            return TypedResults.BadRequest(validationError);
        validationError = ValidateTriviaConfiguration(type, triviaEligible, options, correctAnswers);
        if (!isDraft && validationError is not null)
            return TypedResults.BadRequest(validationError);

        item.ModerationStatus = request.ModerationStatus.Value;
        item.Text = text;
        item.Type = type;
        item.Options = options;
        item.MatchingPairs = matchingPairs;
        item.TriviaEligible = triviaEligible;
        item.CorrectAnswers = correctAnswers;
        item.IsDraft = isDraft;
        item.Explanation = request.Explanation?.Trim() ?? item.Explanation;
        item.Course = request.Course?.Trim() ?? item.Course;
        item.Topic = request.Topic?.Trim() ?? item.Topic;
        if (request.Tags is not null)
            item.Tags = NormalizeTags(request.Tags);
        item.UpdatedAt = DateTime.UtcNow;

        if (!repo.Update(item))
            return TypedResults.BadRequest("Failed to update the question bank item.");

        return TypedResults.Ok(item);
    }

    private static string? ValidateRequest(
        string? text,
        QuestionType? type,
        List<string>? options,
        List<MatchingPair>? matchingPairs,
        QuestionService questionService)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Text is required.";
        if (type is null)
            return "Type is required.";

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = type.Value,
            Options = NormalizeOptions(options),
            MatchingPairs = NormalizePairs(matchingPairs)
        });
        return validation.IsValid ? null : validation.ErrorMessage;
    }

    private static List<string> NormalizeOptions(List<string>? options) =>
        (options ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

    private static List<MatchingPair> NormalizePairs(List<MatchingPair>? pairs) =>
        (pairs ?? [])
            .Select(pair => new MatchingPair
            {
                Prompt = pair?.Prompt?.Trim() ?? string.Empty,
                Match = pair?.Match?.Trim() ?? string.Empty
            })
            .ToList();

    private static List<string> NormalizeTags(List<string>? tags) =>
        (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static string? ValidateTriviaConfiguration(
        QuestionType type,
        bool triviaEligible,
        IReadOnlyCollection<string> options,
        IReadOnlyCollection<string> correctAnswers)
    {
        if (!triviaEligible)
            return type is QuestionType.MultipleChoice or QuestionType.MultipleAnswer &&
                correctAnswers.Any(answer => !options.Contains(answer, StringComparer.Ordinal))
                ? "Correct answers must match the available options."
                : null;
        if (type == QuestionType.Matching)
            return "Matching questions are not currently eligible for trivia.";
        if (correctAnswers.Count == 0)
            return "Trivia questions require at least one correct answer.";

        var allowedAnswers = type == QuestionType.TrueFalse ? ["True", "False"] : options;
        return correctAnswers.All(answer => allowedAnswers.Contains(answer, StringComparer.Ordinal))
            ? null
            : "Correct answers must match the available options.";
    }
}

public class CreateQuestionBankItemRequest
{
    public bool IsDraft { get; init; }
    public string? Explanation { get; init; }
    public string? SourceId { get; init; }
    public string? SourceQuote { get; init; }
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
    public string? Course { get; init; }
    public string? Topic { get; init; }
    public List<string>? Tags { get; init; }
    public bool TriviaEligible { get; init; }
    public List<string>? CorrectAnswers { get; init; }
}

public sealed class SubmitQuestionSuggestionRequest : CreateQuestionBankItemRequest;

public sealed class ModerateQuestionBankItemRequest
{
    public bool? IsDraft { get; init; }
    public string? Explanation { get; init; }
    public QuestionModerationStatus? ModerationStatus { get; init; }
    public string? Text { get; init; }
    public string? Course { get; init; }
    public string? Topic { get; init; }
    public List<string>? Tags { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
    public bool? TriviaEligible { get; init; }
    public List<string>? CorrectAnswers { get; init; }
}

public sealed record QuestionBankListResponse(List<QuestionBankItem> Items, int TotalCount);
