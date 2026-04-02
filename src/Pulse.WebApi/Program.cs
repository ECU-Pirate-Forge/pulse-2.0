using Pulse.Domain.Entities;
using Pulse.WebApi.Middleware;
using Pulse.Common.Services;
using Pulse.WebApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
// builder.Host.UseDefaultServiceProvider((context, options) =>
// {
//     // validate service registrations at build time in development
//     options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
//     options.ValidateOnBuild = true;
// });

var connectionString = builder.Configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";

builder.Services.AddPulseWebApiCoreServices(connectionString);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<InstructorCodeMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () =>
{
    return "Pulse API is running";
});

app.MapGet("/questions", (QuestionRepository repo) =>
{
    return repo.GetAll();
});

app.MapPost("/questions", (QuestionRepository repo, Question q) =>
{
    return repo.Insert(q);
});

app.MapPut("/questions/{id:guid}",
    QuestionEndpointHandlers.UpdateQuestion);

app.MapDelete("/questions/{id:guid}",
    QuestionEndpointHandlers.DeleteQuestion);

app.MapDefaultEndpoints();

app.Run();
