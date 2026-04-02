using Microsoft.AspNetCore.Http;

namespace Pulse.WebApi.Middleware;

public sealed class InstructorCodeMiddleware
{
    public const string HeaderName = "InstructorCode";
    private const string DefaultInstructorCode = "INST001";

    private readonly RequestDelegate _next;
    private readonly string _expectedInstructorCode;

    public InstructorCodeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _expectedInstructorCode = configuration["Security:InstructorCode"] ?? DefaultInstructorCode;
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

        if (!string.Equals(instructorCode.ToString(), _expectedInstructorCode, StringComparison.Ordinal))
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
        if (!path.StartsWithSegments("/questions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsDelete(method);
    }
}