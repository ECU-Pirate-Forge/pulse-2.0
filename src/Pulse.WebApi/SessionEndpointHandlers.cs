using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.Common.Services;
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

        return Results.Ok(new { title = session.Title });
    }

    public static async Task<IResult> CreateSession(
        HttpContext context,
        CreateSessionRequest request,
        ISessionRepository repo,
        IJoinCodeGenerator joinCodeGenerator)
    {
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
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await repo.InsertAsync(session);

        return Results.Created($"/api/sessions/{created.Id}", new CreateSessionResponse(
            created.Id,
            created.JoinCode,
            created.InstructorCode));
    }

    public static async Task<IResult> UnblindSession(
        Guid id,
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
            return Results.StatusCode(403);

        session.IsUnblinded = true;
        session.UpdatedAt = DateTime.UtcNow;
        var updated = repo.Update(session);
        if (!updated)
            return Results.Problem("Failed to persist session update.", statusCode: StatusCodes.Status500InternalServerError);

        // TODO: Emit ResultsUnblinded SignalR event when hub is implemented

        return Results.Ok(session);
    }
}
