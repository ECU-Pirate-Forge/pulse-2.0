using Scalar.Aspire;
using Pulse.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var publicJoinBaseUrl = builder.Configuration["Pulse:PublicJoinBaseUrl"];

if (!string.IsNullOrWhiteSpace(publicJoinBaseUrl))
    publicJoinBaseUrl = PublicJoinUrl.Normalize(publicJoinBaseUrl);

var migration = builder.AddProject<Projects.Pulse_MigrationManager>("migration");

var webapi = builder.AddProject<Projects.Pulse_WebApi>("webapi")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

if (!string.IsNullOrWhiteSpace(publicJoinBaseUrl))
    webapi.WithEnvironment("App__JoinBaseUrl", publicJoinBaseUrl);

var webadmin = builder.AddProject<Projects.Pulse_Admin>("webadmin")
    .WaitFor(webapi)
    .WithReference(webapi)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

if (!string.IsNullOrWhiteSpace(publicJoinBaseUrl))
    webadmin.WithEnvironment("StudentApp__BaseUrl", publicJoinBaseUrl);

var webapp = builder.AddProject<Projects.Pulse_WebApp>("webapp")
    .WaitFor(webapi)
    .WithReference(webapi)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

if (builder.ExecutionContext.IsRunMode && string.IsNullOrWhiteSpace(publicJoinBaseUrl))
{
    var tunnel = builder.AddDevTunnel("pulse-devtunnel",
            tunnelId: builder.Configuration["Pulse:DevTunnelId"])
        .WithAnonymousAccess()
        .WithReference(webapi.GetEndpoint("https"))
        .WithReference(webapp.GetEndpoint("https"));

    var publicStudentEndpoint = tunnel.GetEndpoint(webapp, "https");
    webapi.WithEnvironment("App__JoinBaseUrl", publicStudentEndpoint);
    webadmin.WithEnvironment("StudentApp__BaseUrl", publicStudentEndpoint);
}

var scalar = builder.AddScalarApiReference();

scalar.WithApiReference(webapi);

builder.Build().Run();
