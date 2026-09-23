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
            CreatedAt = DateTime.UtcNow
        };

        var created = repo.Insert(item);
        return TypedResults.Created($"/api/questionbank/{created.Id}", created);
    }

    public static Results<Ok<QuestionBankListResponse>, BadRequest<string>> GetQuestionBankItems(
        IQuestionBankRepository repo,
        string? text = null,
        int? type = null,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1)
            return TypedResults.BadRequest("Page must be greater than 0.");

        if (pageSize < 1)
            return TypedResults.BadRequest("PageSize must be greater than 0.");

        var filtered = repo.Search(text, type).ToList();
        var totalCount = filtered.Count;
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return TypedResults.Ok(new QuestionBankListResponse(items, totalCount));
    }
}

public sealed class CreateQuestionBankItemRequest
{
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
}

public sealed record QuestionBankListResponse(List<QuestionBankItem> Items, int TotalCount);
