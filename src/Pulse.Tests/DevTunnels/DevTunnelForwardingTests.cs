using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelForwardingTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task PublicHostFromLocalProxyUsesConfiguredHttpsOrigin(string peer)
    {
        await using var app = CreateApp(enabled: true, peer);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Host = "pulse-test.use2.devtunnels.ms";
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "http");
        var result = await client.GetStringAsync("/origin", TestContext.Current.CancellationToken);
        Assert.Equal("https://pulse-test.use2.devtunnels.ms", result);
    }

    [Fact]
    public async Task NonLoopbackPeerCannotImpersonateTunnel()
    {
        await using var app = CreateApp(enabled: true, "203.0.113.1");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Host = "pulse-test.use2.devtunnels.ms";
        using var response = await client.GetAsync("/origin", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LocalHealthHopIsNotRedirectedToLocalHttpsPort()
    {
        await using var app = CreateApp(enabled: true, "127.0.0.1");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        Assert.Equal("http://localhost", await client.GetStringAsync("/origin", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisabledModeRetainsHttpsRedirectionAndNoProbeRoute()
    {
        await using var app = CreateApp(enabled: false, "127.0.0.1");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/origin", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https://localhost:7443/origin", response.Headers.Location!.AbsoluteUri);
        using var secureResponse = await client.GetAsync("https://localhost/.well-known/pulse-dev-tunnel", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, secureResponse.StatusCode);
    }

    private static WebApplication CreateApp(bool enabled, string peer)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevTunnels:Enabled"] = enabled.ToString(),
            ["DevTunnels:PublicUrl"] = "https://pulse-test.use2.devtunnels.ms",
            ["DevTunnels:InstanceId"] = "test-instance",
            ["DevTunnels:ServiceName"] = "webapp"
        });
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 7443);
        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            return next(context);
        });
        app.UsePulseDevTunnels();
        app.MapGet("/origin", (HttpRequest request) => $"{request.Scheme}://{request.Host}");
        return app;
    }
}
