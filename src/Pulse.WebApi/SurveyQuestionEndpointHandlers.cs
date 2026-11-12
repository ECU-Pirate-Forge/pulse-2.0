using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi;

public static class SurveyQuestionEndpointHandlers
{
    public static async Task<IResult> GetStudentQuestions(
        Guid sessionId,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound();

        var questions = questionRepo.GetBySessionId(sessionId)
            .OrderBy(question => question.SortOrder);
        if (session.SharedQuestionId is Guid sharedQuestionId)
            questions = questions.Where(question => question.Id == sharedQuestionId).OrderBy(question => question.SortOrder);

        return Results.Ok(questions.Select(StudentQuestionResponse.FromQuestion).ToList());
    }

    public static async Task<IResult> ShareQuestion(
        Guid sessionId,
        Guid questionId,
        HttpContext context,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, context, sessionRepo);
        if (session.Result is not null)
            return session.Result;
        if (session.Value!.Mode != SessionMode.Survey || session.Value.Status != SessionStatus.Active)
            return Results.Conflict("Questions can only be shared while a survey is active.");
        if (questionRepo.GetById(questionId) is not { SessionId: var ownerId } ||
            ownerId != sessionId)
            return Results.NotFound("Question not found in this survey.");

        session.Value.SharedQuestionId = questionId;
        session.Value.UpdatedAt = DateTime.UtcNow;
        if (!sessionRepo.Update(session.Value))
            return Results.Problem("Failed to persist the shared question.", statusCode: StatusCodes.Status500InternalServerError);

        if (eventPublisher is not null)
            await eventPublisher.PublishQuestionsUpdatedAsync(sessionId, context.RequestAborted);

        return Results.NoContent();
    }

    public static async Task<IResult> ClearSharedQuestion(
        Guid sessionId,
        HttpContext context,
        ISessionRepository sessionRepo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, context, sessionRepo);
        if (session.Result is not null)
            return session.Result;

        session.Value!.SharedQuestionId = null;
        session.Value.UpdatedAt = DateTime.UtcNow;
        if (!sessionRepo.Update(session.Value))
            return Results.Problem("Failed to clear the shared question.", statusCode: StatusCodes.Status500InternalServerError);

        if (eventPublisher is not null)
            await eventPublisher.PublishQuestionsUpdatedAsync(sessionId, context.RequestAborted);

        return Results.NoContent();
    }

    public static async Task<IResult> RevealQuestion(
        Guid sessionId,
        Guid questionId,
        HttpContext context,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, context, sessionRepo);
        if (session.Result is not null)
            return session.Result;
        if (questionRepo.GetById(questionId) is not { SessionId: var ownerId } ||
            ownerId != sessionId)
            return Results.NotFound("Question not found in this survey.");

        if (!session.Value!.RevealedQuestionIds.Contains(questionId))
            session.Value.RevealedQuestionIds.Add(questionId);
        session.Value.UpdatedAt = DateTime.UtcNow;
        if (!sessionRepo.Update(session.Value))
            return Results.Problem("Failed to persist the revealed question.", statusCode: StatusCodes.Status500InternalServerError);

        if (eventPublisher is not null)
            await eventPublisher.PublishResultsRevealedAsync(sessionId, context.RequestAborted);

        return Results.NoContent();
    }

    private static async Task<(Session? Value, IResult? Result)> GetOwnedSessionAsync(
        Guid sessionId,
        HttpContext context,
        ISessionRepository sessionRepo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
            return (null, Results.Unauthorized());

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return (null, Results.NotFound());
        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden));

        return (session, null);
    }
}
