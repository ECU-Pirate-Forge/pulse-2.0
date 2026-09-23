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
        if (!validation.IsValid)
            return TypedResults.BadRequest(validation.ErrorMessage!);

        var item = new QuestionBankItem
        {
            Text = request.Text.Trim(),
            Type = request.Type.Value,
            Options = normalizedOptions,
            MatchingPairs = normalizedPairs,
            Course = request.Course?.Trim() ?? string.Empty,
            Topic = request.Topic?.Trim() ?? string.Empty,
            Tags = NormalizeTags(request.Tags),
            ModerationStatus = QuestionModerationStatus.Approved,
            Source = QuestionSource.InstructorAuthored,
            ContributorAttribution = "Instructor",
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
        QuestionBankSort sort = QuestionBankSort.Newest,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1)
            return TypedResults.BadRequest("Page must be greater than 0.");

        if (pageSize < 1)
            return TypedResults.BadRequest("PageSize must be greater than 0.");

        var filtered = repo.Search(new QuestionBankQuery(
            text,
            type,
            course,
            topic,
            tag,
            moderationStatus,
            source,
            sort)).ToList();
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
        var options = request.Options is null ? item.Options : NormalizeOptions(request.Options);
        var matchingPairs = request.MatchingPairs is null
            ? item.MatchingPairs
            : NormalizePairs(request.MatchingPairs);
        var validationError = ValidateRequest(text, type, options, matchingPairs, questionService);
        if (validationError is not null)
            return TypedResults.BadRequest(validationError);

        item.ModerationStatus = request.ModerationStatus.Value;
        item.Text = text;
        item.Type = type;
        item.Options = options;
        item.MatchingPairs = matchingPairs;
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
}

public class CreateQuestionBankItemRequest
{
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
    public string? Course { get; init; }
    public string? Topic { get; init; }
    public List<string>? Tags { get; init; }
}

public sealed class SubmitQuestionSuggestionRequest : CreateQuestionBankItemRequest;

public sealed class ModerateQuestionBankItemRequest
{
    public QuestionModerationStatus? ModerationStatus { get; init; }
    public string? Text { get; init; }
    public string? Course { get; init; }
    public string? Topic { get; init; }
    public List<string>? Tags { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
}

public sealed record QuestionBankListResponse(List<QuestionBankItem> Items, int TotalCount);
