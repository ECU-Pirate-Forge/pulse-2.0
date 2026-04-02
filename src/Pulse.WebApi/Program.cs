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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

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

app.MapGet("/sessions",
    (HttpRequest request, ISessionRepository repo, IConfiguration configuration) =>
        SessionEndpointHandlers.GetSessions(request, repo, configuration));

app.MapDefaultEndpoints();

app.Run();

public static class SessionEndpointHandlers
{
    public static async Task<IResult> GetSessions(HttpRequest request, ISessionRepository repo, IConfiguration configuration)
    {
        var instructorCode = request.Headers[InstructorCodeMiddleware.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(instructorCode))
        {
            return Results.Json(new { error = "InstructorCode is required." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var configuredInstructorCode = configuration["Security:InstructorCode"];
        if (!InstructorCodeMiddleware.IsInstructorCodeValid(instructorCode, configuredInstructorCode))
        {
            return Results.Json(new { error = "InstructorCode is invalid." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var sessions = await repo.GetByInstructorCodeAsync(instructorCode);
        return Results.Ok(sessions);
    }
}
