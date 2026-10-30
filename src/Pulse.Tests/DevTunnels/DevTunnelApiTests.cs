using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Pulse.WebApi;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelApiTests
{
    private const string ScalarOrigin = "https://pulse-scalar.use2.devtunnels.ms";

    [Theory]
    [InlineData(true, ScalarOrigin, true)]
    [InlineData(true, "https://another-site.example", false)]
    [InlineData(false, ScalarOrigin, false)]
    public async Task OnlyConfiguredScalarOriginGetsCorsInTunnelMode(bool enabled, string origin, bool allowed)
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
                .UseSetting("ConnectionStrings:pulse-db", "Filename=:memory:")
                .UseSetting("DevTunnels:Enabled", enabled.ToString())
                .UseSetting("DevTunnels:PublicUrl", "https://pulse-api.use2.devtunnels.ms")
                .UseSetting("DevTunnels:ScalarOrigin", ScalarOrigin)
                .UseSetting("DevTunnels:InstanceId", "cors-test"));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/sessions");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "InstructorCode,Content-Type");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(ScalarOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }
}
