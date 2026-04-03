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

    public static async Task<IResult> GetSessionQr(Guid id, HttpRequest request, ISessionRepository repo, IConfiguration configuration)
    {
        var session = await repo.GetByIdAsync(id);
        if (session is null)
        {
            return Results.NotFound();
        }

        var baseUrl = configuration["App:JoinBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{request.Scheme}://{request.Host}";
        }

        var joinUrl = $"{baseUrl.TrimEnd('/')}/join/{session.JoinCode}";

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(20);

        return Results.File(pngBytes, "image/png");
    }
}
