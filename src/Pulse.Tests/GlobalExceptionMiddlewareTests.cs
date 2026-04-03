using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Pulse.Tests;

public class GlobalExceptionMiddlewareTests
{
    private sealed class TestLogger<T> : ILogger<T>
    {
        public sealed class LogEntry
        {
            public LogLevel Level { get; set; }
            public EventId EventId { get; set; }
            public Exception? Exception { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public readonly List<LogEntry> Entries = new();
        public readonly List<object?> Scopes = new();

        IDisposable Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state)
        {
            Scopes.Add(state);
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry
            {
                Level = logLevel,
                EventId = eventId,
                Exception = exception,
                Message = formatter(state, exception)
            });
        }

        private sealed class NullDisposable : IDisposable { public static readonly NullDisposable Instance = new NullDisposable(); public void Dispose() { } }
    }

    [Fact]
    public async Task InvokeWhenNextThrowsLogsErrorAndReturns500WithCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/test/path";
        context.Request.Method = "POST";

        var logger = new TestLogger<Pulse.WebApi.Middleware.GlobalExceptionMiddleware>();

        RequestDelegate next = _ => throw new InvalidOperationException("boom");

        var middleware = new Pulse.WebApi.Middleware.GlobalExceptionMiddleware(next, logger);

        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-Id"));
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out var cidProp));
        Assert.Equal(correlationId, cidProp.GetString());

        Assert.NotEmpty(logger.Entries);
        var entry = logger.Entries.Find(e => e.Level == LogLevel.Error);
        Assert.NotNull(entry);
        Assert.NotNull(entry.Exception);
        Assert.Contains("Unhandled exception.", entry.Message);

        Assert.NotEmpty(logger.Scopes);
        var scope = logger.Scopes[0] as IDictionary<string, object>;
        Assert.NotNull(scope);
        Assert.Equal(correlationId, scope["CorrelationId"]?.ToString());
        Assert.Equal("/test/path", scope["RequestPath"]?.ToString());
        Assert.Equal("POST", scope["RequestMethod"]?.ToString());
    }
}
