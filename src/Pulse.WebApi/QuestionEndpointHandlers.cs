using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;

namespace Pulse.WebApi;

public static class QuestionEndpointHandlers
{
    public static Results<Ok<Question>, BadRequest<string>> CreateQuestion(Question q, QuestionRepository repo, QuestionService questionService)
    {
        var normalizedOptions = q.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = q.Type,
            Options = normalizedOptions
        });

        if (!validation.IsValid)
            return TypedResults.BadRequest(validation.ErrorMessage!);

        q.Options = normalizedOptions;
        return TypedResults.Ok(repo.Insert(q));
    }

    public static Results<Ok<Question>, BadRequest<string>, NotFound> UpdateQuestion(Guid id, UpdateQuestionRequest request, QuestionRepository repo, QuestionService questionService)
    {
        if (id == Guid.Empty)
            return TypedResults.BadRequest("Question id is invalid.");

        if (string.IsNullOrWhiteSpace(request.Text) || request.Type is null || request.Options is null)
            return TypedResults.BadRequest("Text, type, and options are required.");

        var normalizedOptions = request.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = request.Type.Value,
            Options = normalizedOptions
        });

        if (!validation.IsValid)
        {
            return TypedResults.BadRequest(validation.ErrorMessage!);
        }

        var existing = repo.GetById(id);
        if (existing is null)
            return TypedResults.NotFound();

        existing.Text = request.Text.Trim();
        existing.Type = request.Type.Value;
        existing.Options = normalizedOptions;

        var updateSucceeded = repo.Update(existing);
        if (!updateSucceeded)
            return TypedResults.BadRequest("Failed to update the question.");

        return TypedResults.Ok(existing);
    }

    public static Results<NoContent, BadRequest<string>, NotFound> DeleteQuestion(Guid id, QuestionRepository repo)
    {
        if (id == Guid.Empty)
            return TypedResults.BadRequest("Question id is invalid.");

        var deleted = repo.Delete(id);
        if (!deleted)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }

    public static Results<Ok<List<Question>>, BadRequest<string>> ReorderQuestions(ReorderQuestionsRequest request, QuestionRepository repo)
    {
        if (request.QuestionIds is null || request.QuestionIds.Count == 0)
            return TypedResults.BadRequest("Question ID list is required and must not be empty.");

        var questions = new List<Question>();
        for (int i = 0; i < request.QuestionIds.Count; i++)
        {
            var question = repo.GetById(request.QuestionIds[i]);
            if (question is null)
                return TypedResults.BadRequest($"Question ID {request.QuestionIds[i]} is invalid or not found.");
            question.SortOrder = i;
            questions.Add(question);
        }

        foreach (var question in questions)
            repo.Update(question);

        return TypedResults.Ok(questions);
    }
}

public sealed class UpdateQuestionRequest
{
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
}

public sealed class ReorderQuestionsRequest
{
    public List<Guid> QuestionIds { get; init; } = [];
}
