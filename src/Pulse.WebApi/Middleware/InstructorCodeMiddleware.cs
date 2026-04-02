using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace Pulse.WebApi.Middleware;

public sealed class InstructorCodeMiddleware
{
    public const string HeaderName = "InstructorCode";

    private readonly RequestDelegate _next;
    private readonly string _expectedInstructorCode;

    public InstructorCodeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _expectedInstructorCode = configuration["Security:InstructorCode"]
            ?? throw new InvalidOperationException("Security:InstructorCode is not configured. Provide it via user-secrets or environment variables.");
    }

    public async Task Invoke(HttpContext context)
    {
        if (!InstructorOnlyEndpointMatcher.IsInstructorOnly(context.Request.Method, context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var instructorCode) || string.IsNullOrWhiteSpace(instructorCode))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "InstructorCode is required." });
            return;
        }

        var providedBytes = Encoding.UTF8.GetBytes(instructorCode.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(_expectedInstructorCode);
        if (providedBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "InstructorCode is invalid." });
            return;
        }

        await _next(context);
    }
}

public static class InstructorOnlyEndpointMatcher
{
    public static bool IsInstructorOnly(string method, PathString path)
    {
        if (path.StartsWithSegments("/questions", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(method)
                || HttpMethods.IsPut(method)
                || HttpMethods.IsDelete(method);
        }

        if (path.StartsWithSegments("/sessions", StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            var segments = (remaining.Value ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2
                && (segments[1].Equals("join", StringComparison.OrdinalIgnoreCase)
                    || segments[1].Equals("responses", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        return false;
    }
}