using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi;

public static class QuestionBankImportEndpointHandlers
{
    public static async Task<IResult> ImportQuestions(
        Guid sessionId,
        ImportQuestionsRequest request,
        HttpContext context,
        ISessionRepository sessionRepo,
        IQuestionBankRepository bankRepo,
        QuestionRepository questionRepo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();

        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        if (request.QuestionBankItemIds is null || request.QuestionBankItemIds.Count == 0)
            return Results.BadRequest("Question bank item ID list is required and must not be empty.");

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound("Session not found.");

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(403);

        var bankItems = new List<QuestionBankItem>();
        foreach (var id in request.QuestionBankItemIds)
        {
            var item = bankRepo.GetById(id);
            if (item is null)
                return Results.BadRequest($"Question bank item {id} not found.");
            bankItems.Add(item);
        }

        var createdQuestions = new List<Question>();
        foreach (var item in bankItems)
        {
            var question = new Question
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Text = item.Text,
                Type = item.Type,
                Options = item.Options.ToList(),
                CreatedAt = DateTime.UtcNow
            };
            questionRepo.Insert(question);
            createdQuestions.Add(question);
        }

        return Results.Ok(createdQuestions);
    }
}

public sealed class ImportQuestionsRequest
{
    public List<Guid> QuestionBankItemIds { get; init; } = [];
}
