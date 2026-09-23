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


var scalar = builder.AddScalarApiReference();

scalar.WithApiReference(webapi);

builder.Build().Run();
