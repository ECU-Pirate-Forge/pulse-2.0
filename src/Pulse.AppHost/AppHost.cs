using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var migration = builder.AddProject<Projects.Pulse_MigrationManager>("migration");

var webapi = builder.AddProject<Projects.Pulse_WebApi>("webapi");

var webadmin = builder.AddProject<Projects.Pulse_Admin>("webadmin");

var webapp = builder.AddProject<Projects.Pulse_WebApp>("webapp");

var scalar = builder.AddScalarApiReference(); 

scalar.WithApiReference(webapi);

builder.Build().Run();
