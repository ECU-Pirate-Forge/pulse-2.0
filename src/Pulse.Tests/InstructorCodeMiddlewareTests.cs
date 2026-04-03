using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests;

public class InstructorCodeMiddlewareTests
{
    [Fact]
    public async Task DeleteQuestionMissingInstructorCodeReturns401()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:InstructorCode"] = "INST001"
            })
            .Build();

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new InstructorCodeMiddleware(next, configuration);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Delete;
        context.Request.Path = "/questions/00000000-0000-0000-0000-000000000001";
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("InstructorCode is required.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void IsInstructorOnlyReturnsTrueForProtectedSessionRoute()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/sessions/abc123/results";

        var isProtected = InstructorOnlyEndpointMatcher.IsInstructorOnly(context.Request);

        Assert.True(isProtected);
    }

    [Fact]
    public void IsInstructorOnlyReturnsFalseForPublicStudentRoute()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/sessions/abc123/join";

        var isProtected = InstructorOnlyEndpointMatcher.IsInstructorOnly(context.Request);

        Assert.False(isProtected);
    }
}
