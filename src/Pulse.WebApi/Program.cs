using Pulse.Domain.Entities;
using Pulse.Shared.Models;
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

app.MapPost("/api/sessions", (ISessionRepository repo, CreateSessionRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Title))
        return Results.BadRequest(new { error = "Title is required." });

    var now = DateTime.UtcNow;
    var session = new Session
    {
        Title = req.Title,
        JoinCode = GenerateCode(6),
        InstructorCode = GenerateCode(8),
        Status = "Draft",
        CreatedAt = now,
        UpdatedAt = now
    };

    repo.Insert(session);

    return Results.Created(
        $"/api/sessions/{session.Id}",
        new CreateSessionResponse(session.Id, session.JoinCode, session.InstructorCode));
});

app.MapGet("/api/sessions/{id:guid}", (ISessionRepository repo, Guid id, HttpContext ctx) =>
{
    var instructorCode = ctx.Request.Headers["InstructorCode"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(instructorCode))
        return Results.Unauthorized();

    var session = repo.GetById(id);
    if (session is null)
        return Results.NotFound();

    if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
        return Results.StatusCode(403);

    return Results.Ok(session);
});

app.MapPut("/questions/{id:guid}",
    QuestionEndpointHandlers.UpdateQuestion);

app.MapDelete("/questions/{id:guid}",
    QuestionEndpointHandlers.DeleteQuestion);

app.MapGet("/sessions", SessionEndpointHandlers.GetSessions);

app.MapDefaultEndpoints();

app.Run();

static string GenerateCode(int length)
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    return new string(Enumerable.Range(0, length)
        .Select(_ => chars[Random.Shared.Next(chars.Length)])
        .ToArray());
}

record CreateSessionRequest(string Title);
record CreateSessionResponse(Guid Id, string JoinCode, string InstructorCode);

public static class SessionEndpointHandlers
{
    public static async Task<IResult> GetSessions(HttpContext context, ISessionRepository repo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString()
            ?? throw new InvalidOperationException(
                "InstructorCode was not set by InstructorCodeMiddleware. Ensure the middleware is registered.");
        var sessions = await repo.GetByInstructorCodeAsync(instructorCode);
        return Results.Ok(sessions);
    }
}

public partial class Program { }
