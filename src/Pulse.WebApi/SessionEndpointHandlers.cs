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

    public static async Task<IResult> JoinSessionByCode(string joinCode, ISessionRepository repo)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return Results.BadRequest(new { error = "Join code is required." });

        var session = await repo.GetByJoinCodeAsync(joinCode);
        if (session == null)
            return Results.NotFound(new { error = "Session not found. Please check your code." });

        return Results.Ok(new { title = session.Title });
    }
}
