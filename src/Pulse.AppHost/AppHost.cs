using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var migration = builder.AddProject<Projects.Pulse_MigrationManager>("migration");

var webapi = builder.AddProject<Projects.Pulse_WebApi>("webapi")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var webadmin = builder.AddProject<Projects.Pulse_Admin>("webadmin")
    .WaitFor(webapi)
    .WithReference(webapi)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var webapp = builder.AddProject<Projects.Pulse_WebApp>("webapp")
    .WaitFor(webapi)
    .WithReference(webapi)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");


var scalar = builder.AddScalarApiReference();

scalar.WithApiReference(webapi);

builder.Build().Run();
