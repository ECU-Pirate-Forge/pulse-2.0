using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
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
app.UseMiddleware<InstructorCodeMiddleware>();

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

app.MapDelete("/questions/{id:guid}",
    QuestionEndpointHandlers.DeleteQuestion);

app.MapGet("/sessions", SessionEndpointHandlers.GetSessions);
app.MapPost("/api/sessions", SessionEndpointHandlers.CreateSession);

app.MapDefaultEndpoints();

app.Run();

public static class SessionEndpointHandlers
{
    public static async Task<IResult> CreateSession(
        HttpContext context,
        CreateSessionRequest request,
        ISessionRepository repo,
        IJoinCodeGenerator joinCodeGenerator)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");

        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware.");

        string joinCode;
        do
        {
            joinCode = joinCodeGenerator.Generate();
        } while (await repo.JoinCodeExistsAsync(joinCode));

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            JoinCode = joinCode,
            InstructorCode = instructorCode,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await repo.InsertAsync(session);

        return Results.Created($"/api/sessions/{created.Id}", new CreateSessionResponse
        {
            Id = created.Id,
            JoinCode = created.JoinCode,
            InstructorCode = created.InstructorCode
        });
    }

    public static async Task<IResult> GetSessions(HttpContext context, ISessionRepository repo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware. Ensure the middleware is registered.");
        var sessions = await repo.GetByInstructorCodeAsync(instructorCode);
        return Results.Ok(sessions);
    }
}
