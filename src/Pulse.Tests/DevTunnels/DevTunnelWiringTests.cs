using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pulse.AppHost;
using Pulse.AppHost.DevTunnels;
using Scalar.Aspire;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelWiringTests
{
    [Fact]
    public async Task AppHostUsesLocalOllamaWithoutProvisioningAnOllamaTunnel()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Projects.Pulse_AppHost), ["--DevTunnels:Enabled=false"],
            cancellationToken: TestContext.Current.CancellationToken);
        var api = Assert.IsType<ProjectResource>(builder.Resources.Single(resource => resource.Name == "webapi"));
        var environment = await ReadEnvironmentAsync(builder, api);

        Assert.Equal("http://localhost:11434/v1", environment["Ollama__BaseUrl"]);
        Assert.DoesNotContain("ollama", builder.Configuration.GetSection("DevTunnels:Services")
            .GetChildren().Select(service => service.Key));
        Assert.DoesNotContain("ollama", DevTunnelOptions.ServiceNames);
    }

    [Fact]
    public async Task DisabledAppHostBuildsExistingGraphWithoutTunnelEnvironment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Projects.Pulse_AppHost), ["--DevTunnels:Enabled=false"], cancellationToken: cancellationToken);
        Assert.Equal(["migration","migration-rebuilder", "scalar", "webadmin","webadmin-rebuilder", "webapi", "webapi-rebuilder", "webapp", "webapp-rebuilder"],
            builder.Resources.Select(resource => resource.Name).Order().ToArray());
        var webapp = Assert.IsType<ProjectResource>(builder.Resources.Single(resource => resource.Name == "webapp"));
        var environment = await ReadEnvironmentAsync(builder, webapp);
        Assert.DoesNotContain("DevTunnels__Enabled", environment.Keys);
        Assert.DoesNotContain("App__JoinBaseUrl", environment.Keys);
    }

    [Fact]
    public async Task EnabledWiringPinsPortsAndOverridesLocalhostUrlsIncludingScalar()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = DevTunnelOptionsTests.CreateOptions();
        foreach (var service in options.Services.Values)
            service.TunnelId = "test-" + Guid.NewGuid().ToString("N");
        var cli = new DevTunnelManagerTests.FakeCli();
        using var http = new HttpClient();
        await using var manager = new DevTunnelManager(options, cli, http, NullLogger.Instance);
        await manager.PrepareAsync(cancellationToken);
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Projects.Pulse_AppHost), ["--DevTunnels:Enabled=false"], cancellationToken: cancellationToken);
        var api = builder.CreateResourceBuilder<ProjectResource>("webapi");
        var app = builder.CreateResourceBuilder<ProjectResource>("webapp");
        var admin = builder.CreateResourceBuilder<ProjectResource>("webadmin");
        var scalar = builder.AddScalarApiReference("test-scalar", scalarOptions => scalarOptions.DisableDefaultProxy());
        DevTunnelWiring.ConfigureApplication(api, options, manager);
        DevTunnelWiring.ConfigureApplication(app, options, manager);
        DevTunnelWiring.ConfigureApplication(admin, options, manager);
        //DevTunnelWiring.ConfigureScalar(scalar, api, options, manager);
        await using var application = await builder.BuildAsync(cancellationToken);

        foreach (var resource in new[] { api.Resource, app.Resource, admin.Resource })
        {
            var environment = await ReadEnvironmentAsync(builder, resource);
            Assert.Equal(manager.PublicUrls["webapi"].GetLeftPart(UriPartial.Authority), environment["WebApi__BaseUrl"]);
            Assert.Equal(manager.PublicUrls["webapp"].GetLeftPart(UriPartial.Authority), environment["App__JoinBaseUrl"]);
            Assert.Equal(manager.InstanceId, environment["DevTunnels__InstanceId"]);
            Assert.Equal(manager.PublicUrls["scalar"].GetLeftPart(UriPartial.Authority), environment["DevTunnels__ScalarOrigin"]);
            Assert.Equal(options.Services[resource.Name].Port,
                resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http").Port);
        }

        var scalarEnvironment = await ReadEnvironmentAsync(builder, scalar.Resource);
        var configuration = string.Join(" ", scalarEnvironment.Values);
        Assert.Contains(manager.InstanceId, configuration);
        Assert.Contains(manager.PublicUrls["webapi"].Host, configuration);
        Assert.DoesNotContain("/scalar-proxy", configuration);
        Assert.False(Assert.IsType<bool>(scalarEnvironment["DEFAULT_PROXY"]));
        Assert.DoesNotContain("localhost", configuration);
        Assert.DoesNotContain("http://webapi", configuration);
    }

    private static async Task<Dictionary<string, object>> ReadEnvironmentAsync(
        IDistributedApplicationBuilder builder, IResource resource)
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray())
            await callback.Callback(context);
        return environment;
    }
}
