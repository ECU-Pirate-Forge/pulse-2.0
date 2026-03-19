using System.Text.Json;

namespace Pulse.WebApi.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
                context.TraceIdentifier = correlationId;
            }

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path.Value ?? string.Empty,
                ["RequestMethod"] = context.Request.Method
            }))
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception. CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}",
                    correlationId,
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty);
            }

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Correlation-Id"] = correlationId;

                var payload = new
                {
                    error = "An unexpected error occurred.",
                    correlationId
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
