using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class QuestionBankEndpointHandlers
{
    public static Results<Created<QuestionBankItem>, BadRequest<string>> CreateQuestionBankItem(
        CreateQuestionBankItemRequest request,
        IQuestionBankRepository repo)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return TypedResults.BadRequest("Text is required.");

        if (request.Type is null)
            return TypedResults.BadRequest("Type is required.");

        if (!Enum.IsDefined(typeof(QuestionType), request.Type))
            return TypedResults.BadRequest("Type is invalid.");

        var normalizedOptions = (request.Options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .ToList();

        if (request.Type == QuestionType.MultipleChoice && normalizedOptions.Count < 2)
            return TypedResults.BadRequest("Multiple-choice items must include at least 2 options.");

        var item = new QuestionBankItem
        {
            Text = request.Text.Trim(),
            Type = request.Type.Value,
            Options = normalizedOptions,
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
}

public sealed record QuestionBankListResponse(List<QuestionBankItem> Items, int TotalCount);
