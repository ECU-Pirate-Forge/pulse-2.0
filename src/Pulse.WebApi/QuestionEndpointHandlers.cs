using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi;

public static class QuestionEndpointHandlers
{
    public static async Task<IResult> CreateQuestionForSession(
        Guid sessionId,
        Question question,
        QuestionRepository questionRepo,
        QuestionService questionService,
        ISessionRepository sessionRepo,
        HttpContext context)
    {
        var accessResult = await ValidateDraftSession(sessionId, sessionRepo, context);
        if (accessResult is not null)
            return accessResult;

        question.SessionId = sessionId;
        var validationError = NormalizeAndValidate(question, questionService);
        if (validationError is not null)
            return Results.BadRequest(validationError);

        question.Text = question.Text.Trim();
        var created = questionRepo.Insert(question);
        return Results.Created($"/api/sessions/{sessionId}/questions/{created.Id}", created);
    }

    public static async Task<IResult> UpdateQuestionForInstructor(
        Guid id,
        UpdateQuestionRequest request,
        QuestionRepository questionRepo,
        QuestionService questionService,
        ISessionRepository sessionRepo,
        HttpContext context)
    {
        if (id == Guid.Empty)
            return Results.BadRequest("Question id is invalid.");

        var existing = questionRepo.GetById(id);
        if (existing is null)
            return Results.NotFound();

        var accessResult = await ValidateDraftSession(existing.SessionId, sessionRepo, context);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Text) || request.Type is null)
            return Results.BadRequest("Text and type are required.");

        var candidate = new Question
        {
            Id = existing.Id,
            SessionId = existing.SessionId,
            Text = request.Text,
            Type = request.Type.Value,
            Options = request.Options ?? [],
            MatchingPairs = request.MatchingPairs ?? [],
            SortOrder = existing.SortOrder,
            CreatedAt = existing.CreatedAt
        };

        var validationError = NormalizeAndValidate(candidate, questionService);
        if (validationError is not null)
            return Results.BadRequest(validationError);

        candidate.Text = candidate.Text.Trim();
        if (!questionRepo.Update(candidate))
            return Results.BadRequest("Failed to update the question.");

        return Results.Ok(candidate);
    }

    public static async Task<IResult> DeleteQuestionForInstructor(
        Guid id,
        QuestionRepository questionRepo,
        ISessionRepository sessionRepo,
        HttpContext context)
    {
        if (id == Guid.Empty)
            return Results.BadRequest("Question id is invalid.");

        var existing = questionRepo.GetById(id);
        if (existing is null)
            return Results.NotFound();

        var accessResult = await ValidateDraftSession(existing.SessionId, sessionRepo, context);
        if (accessResult is not null)
            return accessResult;

        return questionRepo.Delete(id) ? Results.NoContent() : Results.NotFound();
    }

    public static async Task<IResult> ReorderQuestionsForInstructor(
        ReorderQuestionsRequest request,
        QuestionRepository questionRepo,
        ISessionRepository sessionRepo,
        HttpContext context)
    {
        if (request.QuestionIds is null || request.QuestionIds.Count == 0)
            return Results.BadRequest("Question ID list is required and must not be empty.");

        var questions = new List<Question>();
        foreach (var questionId in request.QuestionIds)
        {
            var question = questionRepo.GetById(questionId);
            if (question is null)
                return Results.BadRequest($"Question ID {questionId} is invalid or not found.");
            questions.Add(question);
        }

        var sessionId = questions[0].SessionId;
        if (questions.Any(question => question.SessionId != sessionId))
            return Results.BadRequest("All reordered questions must belong to the same session.");

        var accessResult = await ValidateDraftSession(sessionId, sessionRepo, context);
        if (accessResult is not null)
            return accessResult;

        for (var index = 0; index < questions.Count; index++)
        {
            questions[index].SortOrder = index;
            questionRepo.Update(questions[index]);
        }

        return Results.Ok(questions);
    }

    public static Results<Ok<Question>, BadRequest<string>> CreateQuestion(Question q, QuestionRepository repo, QuestionService questionService)
    {
        var normalizedOptions = q.Options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = q.Type,
            Options = normalizedOptions,
            MatchingPairs = q.MatchingPairs
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
            Options = normalizedOptions,
            MatchingPairs = request.MatchingPairs ?? []
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
        existing.MatchingPairs = NormalizeMatchingPairs(request.MatchingPairs ?? []);

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

    private static string? NormalizeAndValidate(Question question, QuestionService questionService)
    {
        question.Options = (question.Options ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToList();
        question.MatchingPairs = NormalizeMatchingPairs(question.MatchingPairs ?? []);

        var validation = questionService.ValidateQuestion(new QuestionDTO
        {
            Type = question.Type,
            Options = question.Options,
            MatchingPairs = question.MatchingPairs
        });

        return validation.IsValid ? null : validation.ErrorMessage;
    }

    private static List<MatchingPair> NormalizeMatchingPairs(IEnumerable<MatchingPair> pairs) =>
        pairs.Select(pair => new MatchingPair
        {
            Prompt = pair?.Prompt?.Trim() ?? string.Empty,
            Match = pair?.Match?.Trim() ?? string.Empty
        }).ToList();

    private static async Task<IResult?> ValidateDraftSession(
        Guid sessionId,
        ISessionRepository sessionRepo,
        HttpContext context)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
        {
            return Results.Json(
                new { error = "InstructorCode is required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound("Session not found.");

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (session.Status != SessionStatus.Draft)
            return Results.Conflict("Questions can only be changed while the survey is in Draft.");

        return null;
    }
}

public sealed class UpdateQuestionRequest
{
    public string? Text { get; init; }
    public QuestionType? Type { get; init; }
    public List<string>? Options { get; init; }
    public List<MatchingPair>? MatchingPairs { get; init; }
}

public sealed class ReorderQuestionsRequest
{
    public List<Guid> QuestionIds { get; init; } = [];
}
