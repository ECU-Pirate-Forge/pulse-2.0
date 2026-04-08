using Microsoft.AspNetCore.Http;
using Pulse.WebApi;

namespace Pulse.Tests.Sessions;

public class BuildJoinUrlTests
{
    [Fact]
    public void BuildJoinUrl_UsesConfiguredBaseUrl()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5000);

        var url = SessionEndpointHandlers.BuildJoinUrl("https://pulse.example", context.Request, "ABC123");

        Assert.Equal("https://pulse.example/join/ABC123", url);
    }

    [Fact]
    public void BuildJoinUrl_TrimsTrailingSlashFromConfiguredBaseUrl()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5000);

        var url = SessionEndpointHandlers.BuildJoinUrl("https://pulse.example/", context.Request, "ABC123");

        Assert.Equal("https://pulse.example/join/ABC123", url);
    }

    [Fact]
    public void BuildJoinUrl_FallsBackToRequestHostWhenConfigIsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("pulse.example");

        var url = SessionEndpointHandlers.BuildJoinUrl(null, context.Request, "XYZ999");

        Assert.Equal("https://pulse.example/join/XYZ999", url);
    }

    [Fact]
    public void BuildJoinUrl_FallsBackToRequestHostWhenConfigIsWhitespace()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("pulse.example");

        var url = SessionEndpointHandlers.BuildJoinUrl("   ", context.Request, "XYZ999");

        Assert.Equal("https://pulse.example/join/XYZ999", url);
    }
}
