using Aspire.Hosting.ApplicationModel;
using Scalar.Aspire;

namespace Pulse.AppHost.DevTunnels;

public static class DevTunnelWiring
{
    public static void ConfigureApplication(
        IResourceBuilder<ProjectResource> resource, DevTunnelOptions options, DevTunnelManager tunnels)
    {
        var name = resource.Resource.Name;
        var publicUrl = tunnels.PublicUrls[name].GetLeftPart(UriPartial.Authority);
        resource.WithEndpoint("http", endpoint => endpoint.Port = options.Services[name].Port)
            .WithEnvironment("DevTunnels__Enabled", "true")
            .WithEnvironment("DevTunnels__PublicUrl", publicUrl)
            .WithEnvironment("DevTunnels__InstanceId", tunnels.InstanceId)
            .WithEnvironment("DevTunnels__ServiceName", name)
            .WithEnvironment("DevTunnels__ScalarOrigin", tunnels.PublicUrls["scalar"].GetLeftPart(UriPartial.Authority))
            .WithEnvironment("WebApi__BaseUrl", tunnels.PublicUrls["webapi"].GetLeftPart(UriPartial.Authority))
            .WithEnvironment("App__JoinBaseUrl", tunnels.PublicUrls["webapp"].GetLeftPart(UriPartial.Authority))
            .WithUrls(context =>
            {
                context.Urls.Clear();
                context.Urls.Add(new() { Url = publicUrl, DisplayText = "Public dev tunnel" });
            });
    }

    internal sealed class DevTunnelStatusResource(string name) : Resource(name), IResourceWithWaitSupport;

    public static void ConfigureScalar(
        IResourceBuilder<ScalarResource> scalar, IResourceBuilder<ProjectResource> webapi,
        DevTunnelOptions options, DevTunnelManager tunnels)
    {
        var publicUrl = tunnels.PublicUrls["scalar"].GetLeftPart(UriPartial.Authority);
        var apiUrl = tunnels.PublicUrls["webapi"].GetLeftPart(UriPartial.Authority);
        scalar.WithEndpoint("http", endpoint => endpoint.Port = options.Services["scalar"].Port)
            .WithUrls(context =>
            {
                context.Urls.Clear();
                context.Urls.Add(new() { Url = publicUrl, DisplayText = "Public API reference" });
            })
            .WithApiReference(webapi, scalarOptions =>
            {
                scalarOptions.WithBaseDocumentUrl(ReferenceExpression.Empty)
                    .WithOpenApiRoutePattern($"{apiUrl}/openapi/{{documentName}}.json")
                    .AddServer(apiUrl)
                    .WithBaseServerUrl(apiUrl)
                    .AddMetadata("pulseInstance", tunnels.InstanceId);
            });
    }
}
