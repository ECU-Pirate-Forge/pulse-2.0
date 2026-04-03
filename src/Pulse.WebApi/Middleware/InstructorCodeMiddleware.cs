using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Pulse.WebApi.Middleware;

public sealed class InstructorCodeMiddleware
{
    public const string HeaderName = "InstructorCode";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public InstructorCodeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!InstructorOnlyEndpointMatcher.IsInstructorOnly(context.Request))
        {
            await _next(context);
            return;
        }

        var instructorCode = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(instructorCode))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "InstructorCode is required." });
            return;
        }

        var configuredInstructorCode = _configuration["Security:InstructorCode"];
        if (!IsInstructorCodeValid(instructorCode, configuredInstructorCode))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "InstructorCode is invalid." });
            return;
        }

        context.Items[HeaderName] = instructorCode;
        await _next(context);
    }

    public static bool IsInstructorCodeValid(string? instructorCode, string? configuredInstructorCode)
    {
        if (string.IsNullOrWhiteSpace(instructorCode) || string.IsNullOrWhiteSpace(configuredInstructorCode))
        {
            return false;
        }

        var instructorCodeHash = SHA256.HashData(Encoding.UTF8.GetBytes(instructorCode));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredInstructorCode));
        return CryptographicOperations.FixedTimeEquals(instructorCodeHash, configuredHash);
    }
}

public static class InstructorOnlyEndpointMatcher
{
    private static readonly Regex SessionIdRouteRegex = new("^/sessions/[^/]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SessionResultsRouteRegex = new("^/sessions/[^/]+/results$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SessionQrRouteRegex = new("^/sessions/[^/]+/(qr|qr-code)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PublicStudentSessionRouteRegex = new("^/sessions/[^/]+/(join|responses)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsInstructorOnly(HttpRequest request)
    {
        var method = request.Method;
        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        if (HttpMethods.IsGet(method) && path == "/")
        {
            return false;
        }

        if (HttpMethods.IsGet(method) && path.StartsWith("/questions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HttpMethods.IsPost(method) && PublicStudentSessionRouteRegex.IsMatch(path))
        {
            return false;
        }

        if (path.StartsWith("/questions", StringComparison.OrdinalIgnoreCase)
            && (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method)))
        {
            return true;
        }

        if ((HttpMethods.IsPost(method) || HttpMethods.IsGet(method))
            && (path.Equals("/sessions", StringComparison.OrdinalIgnoreCase) || path.Equals("/api/sessions", StringComparison.OrdinalIgnoreCase)))        {
            return true;
        }

        if ((HttpMethods.IsPut(method) || HttpMethods.IsDelete(method))
            && SessionIdRouteRegex.IsMatch(path))
        {
            return true;
        }

        if (HttpMethods.IsGet(method) && (SessionResultsRouteRegex.IsMatch(path) || SessionQrRouteRegex.IsMatch(path)))
        {
            return true;
        }

        return false;
    }
}
