using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class ResponseEndpointHandlers
{
    public static async Task<IResult> Respond(
        Guid sessionId,
        Guid questionId,
        RespondRequest request,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        IResponseRepository responseRepo,
        DeviceIdValidationService deviceIdService,
        ILoggerFactory loggerFactory,
        HttpContext context)
    {
        var logger = loggerFactory.CreateLogger("ResponseEndpointHandlers");
        var deviceValidation = deviceIdService.ValidateDeviceId(request.DeviceId);
        if (!deviceValidation.IsValid)
            return Results.BadRequest(deviceValidation.ErrorMessage);

        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest("Value is required.");

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound("Session not found.");

        if (!string.Equals(session.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Response rejected — session not active SessionId={SessionId} CorrelationId={CorrelationId}", sessionId, context.TraceIdentifier);
            return Results.Conflict("Session is not active.");
        }

        var question = questionRepo.GetById(questionId);
        if (question is null)
            return Results.NotFound("Question not found.");

        var response = new Response
        {
            QuestionId = questionId,
            SessionId = sessionId,
            DeviceId = request.DeviceId!,
            Value = request.Value,
            SubmittedAt = DateTime.UtcNow
        };

        responseRepo.Upsert(response);

        logger.LogInformation("Response submitted SessionId={SessionId} QuestionId={QuestionId} DeviceId={DeviceId} CorrelationId={CorrelationId}",
            sessionId, questionId, request.DeviceId, context.TraceIdentifier);

        return Results.Ok(new RespondResult(response.SubmittedAt));
    }
}

public sealed class RespondRequest
{
    public string? DeviceId { get; init; }
    public string? Value { get; init; }
}

public sealed record RespondResult(DateTime SubmittedAt);
