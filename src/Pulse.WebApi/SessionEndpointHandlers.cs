using Pulse.Application.Services;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.Common.Services;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Resources;
using Pulse.WebApi.Middleware;
using QRCoder;

namespace Pulse.WebApi;

public static class SessionEndpointHandlers
{
    public static async Task<IResult> GetSessions(HttpContext context, ISessionRepository repo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware. Ensure the middleware is registered.");
        var sessions = await repo.GetByInstructorCodeAsync(instructorCode);
        return Results.Ok(sessions);
    }

    public static string BuildJoinUrl(string? configuredBaseUrl, HttpRequest request, string joinCode)
    {
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : configuredBaseUrl;

        return $"{baseUrl.TrimEnd('/')}/join/{joinCode}";
    }

    public static async Task<IResult> GetSessionQr(Guid id, HttpRequest request, ISessionRepository repo, IConfiguration configuration)
    {
        var session = await repo.GetByIdAsync(id);
        if (session is null)
        {
            return Results.NotFound();
        }

        var joinUrl = BuildJoinUrl(configuration["App:JoinBaseUrl"], request, session.JoinCode);

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(20);

        return Results.File(pngBytes, "image/png");
    }

    public static async Task<IResult> GetSessionQrByCode(string joinCode, HttpRequest request, ISessionRepository repo, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return Results.BadRequest(new { error = "Join code is required." });

        var session = await repo.GetByJoinCodeAsync(joinCode);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        var joinUrl = BuildJoinUrl(configuration["App:JoinBaseUrl"], request, session.JoinCode);

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(20);

        return Results.File(pngBytes, "image/png");
    }

    public static async Task<IResult> JoinSessionByCode(string joinCode, ISessionRepository repo)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return Results.BadRequest(new { error = "Join code is required." });

        var session = await repo.GetByJoinCodeAsync(joinCode);
        if (session == null)
            return Results.NotFound(new { error = "Session not found. Please check your code." });

        if (session.Status != SessionStatus.Active)
            return Results.Conflict(new { error = "This survey is not accepting responses." });

        return Results.Ok(new { id = session.Id, title = session.Title, joinCode = session.JoinCode, resetVersion = session.ResetVersion });
    }

    public static async Task<IResult> CreateSession(
        HttpContext context,
        CreateSessionRequest request,
        ISessionRepository repo,
        IJoinCodeGenerator joinCodeGenerator,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SessionEndpointHandlers");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");

        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware.");

        string joinCode;
        do
        {
            joinCode = joinCodeGenerator.Generate();
        } while (await repo.JoinCodeExistsAsync(joinCode));

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            JoinCode = joinCode,
            InstructorCode = instructorCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await repo.InsertAsync(session);

        logger.LogInformation("Session created SessionId={SessionId} Title={Title} CorrelationId={CorrelationId}",
            created.Id, request.Title, context.TraceIdentifier);

        return Results.Created($"/api/sessions/{created.Id}", new CreateSessionResponse(
            created.Id,
            created.JoinCode,
            created.InstructorCode));
    }

    public static async Task<IResult> DeleteSession(
        Guid id,
        HttpContext context,
        ISessionRepository repo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware.");

        var session = await repo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return await repo.DeleteAsync(id)
            ? Results.NoContent()
            : Results.NotFound();
    }

    public static async Task<IResult> UnblindSession(
        Guid id,
        HttpContext context,
        ISessionRepository repo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();

        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        var session = await repo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(403);

        session.ResultsVisibility = ResultsVisibility.Revealed;
        session.UpdatedAt = DateTime.UtcNow;
        var updated = repo.Update(session);
        if (!updated)
            return Results.Problem("Failed to persist session update.", statusCode: StatusCodes.Status500InternalServerError);

        if (eventPublisher is not null)
            await eventPublisher.PublishResultsRevealedAsync(id, context.RequestAborted);

        return Results.Ok(session);
    }

    /// <summary>Clears a survey run for its owning instructor while preserving reusable content.</summary>
    public static async Task<IResult> ResetSurveyAsync(
        Guid id,
        HttpContext context,
        ISessionRepository repo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        var session = await repo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();
        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (session.Mode != SessionMode.Survey)
            return Results.BadRequest(new { error = SurveyText.SurveyOnlyReset });
        if (!repo.ResetSurvey(id))
            return Results.Problem(SurveyText.ResetFailed, statusCode: StatusCodes.Status500InternalServerError);

        if (eventPublisher is not null)
            await eventPublisher.PublishSubmissionUpdatedAsync(id, new SessionProgressUpdate(0, 0), context.RequestAborted);

        return Results.NoContent();
    }

    /// <summary>Pauses an instructor-owned survey for editing, requiring consent before clearing responses.</summary>
    public static async Task<IResult> BeginSurveyEditAsync(
        Guid id,
        HttpContext context,
        ISessionRepository repo,
        bool clearResponses = false,
        ISessionEventPublisher? eventPublisher = null)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        var session = await repo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();
        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (session.Mode != SessionMode.Survey)
            return Results.BadRequest(new { error = SurveyText.SurveyOnlyEdit });
        if (!repo.BeginSurveyEdit(id, clearResponses))
            return Results.Conflict(new { error = SurveyText.EditConfirmation });

        if (eventPublisher is not null)
            await eventPublisher.PublishSubmissionUpdatedAsync(id, new SessionProgressUpdate(0, 0), context.RequestAborted);

        return Results.NoContent();
    }

    public static Task<IResult> ActivateSession(
        Guid id,
        HttpContext context,
        ISessionRepository repo) =>
        TransitionSession(id, SessionStatus.Active, context, repo);

    public static async Task<IResult> CloseSession(
        Guid id,
        HttpContext context,
        ISessionRepository repo,
        ISessionEventPublisher? eventPublisher = null)
    {
        var result = await TransitionSession(id, SessionStatus.Closed, context, repo);
        if (eventPublisher is not null &&
            result is Microsoft.AspNetCore.Http.HttpResults.Ok<Session>)
        {
            await eventPublisher.PublishSessionClosedAsync(id, context.RequestAborted);
        }

        return result;
    }

    private static async Task<IResult> TransitionSession(
        Guid id,
        SessionStatus nextStatus,
        HttpContext context,
        ISessionRepository repo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        var session = await repo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!SessionLifecycleService.CanTransition(session.Status, nextStatus))
        {
            return Results.Conflict(
                $"Cannot transition a {session.Status} session to {nextStatus}.");
        }

        session.Status = nextStatus;
        session.UpdatedAt = DateTime.UtcNow;
        if (!repo.Update(session))
        {
            return Results.Problem(
                "Failed to persist session update.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(session);
    }
}
