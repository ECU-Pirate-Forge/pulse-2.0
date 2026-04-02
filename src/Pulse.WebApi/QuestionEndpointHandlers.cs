using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Common.Services;
using Pulse.Domain.Entities;

namespace Pulse.WebApi;

public static class QuestionEndpointHandlers
{
    public static Results<Ok<Question>, BadRequest<string>, NotFound> UpdateQuestion(Guid id, UpdateQuestionRequest request, QuestionRepository repo)
    {
        if (id == Guid.Empty)
        {
            return TypedResults.BadRequest("Question id is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Text) || request.Type is null || request.Options is null)
        {
            return TypedResults.BadRequest("Text, type, and options are required.");
        }

        var normalizedOptions = request.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

        if (request.Type == QuestionType.MultipleChoice && normalizedOptions.Count < 2)
        {
            return TypedResults.BadRequest("Multiple-choice questions must include at least 2 options.");
        }

        var existing = repo.GetById(id);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        existing.Text = request.Text.Trim();
        existing.Type = request.Type.Value;
        existing.Options = normalizedOptions;

        var updateSucceeded = repo.Update(existing);
        if (!updateSucceeded)
        {
            return TypedResults.BadRequest("Failed to update the question.");
        }

        return TypedResults.Ok(existing);
    }
}

public sealed class UpdateQuestionRequest
{
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
}
