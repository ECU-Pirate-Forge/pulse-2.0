using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests;

public class InstructorCodeMiddlewareTests
{
    [Fact]
    public async Task ProtectedRouteMissingInstructorCodeReturns401AndStopsPipeline()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("INST001", _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/questions";

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRouteInvalidInstructorCodeReturns403AndStopsPipeline()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("INST001", _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = "/questions/11111111-1111-1111-1111-111111111111";
        context.Request.Headers[InstructorCodeMiddleware.HeaderName] = "BADCODE";

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRouteBlankInstructorCodeReturns401AndStopsPipeline()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("INST001", _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/questions";
        context.Request.Headers[InstructorCodeMiddleware.HeaderName] = "   ";

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRouteValidInstructorCodeContinuesRequest()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("INST001", context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/questions";
        httpContext.Request.Headers[InstructorCodeMiddleware.HeaderName] = "INST001";

        await middleware.Invoke(httpContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task PublicRouteWithoutInstructorCodeRemainsAccessible()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("INST001", context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/questions";

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/questions", true)]
    [InlineData("PUT", "/questions/11111111-1111-1111-1111-111111111111", true)]
    [InlineData("DELETE", "/questions/11111111-1111-1111-1111-111111111111", true)]
    [InlineData("GET", "/questions", false)]
    [InlineData("GET", "/", false)]
    [InlineData("POST", "/sessions", false)]
    public void InstructorOnlyMatcherIdentifiesIntendedRoutes(string method, string path, bool expected)
    {
        var isProtected = InstructorOnlyEndpointMatcher.IsInstructorOnly(method, new PathString(path));

        Assert.Equal(expected, isProtected);
    }

    [Fact]
    public void MiddlewareThrowsWhenSecurityInstructorCodeConfigIsMissing()
    {
        var config = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new InstructorCodeMiddleware(_ => Task.CompletedTask, config));

        Assert.Contains("Security:InstructorCode", ex.Message);
    }

    private static InstructorCodeMiddleware CreateMiddleware(string expectedCode, RequestDelegate next)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:InstructorCode"] = expectedCode
            })
            .Build();

        return new InstructorCodeMiddleware(next, config);
    }
}