using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;
using Pulse.Common.Services;
using Pulse.WebApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";

builder.Services.AddPulseWebApiCoreServices(connectionString);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InstructorCodeMiddleware>();
app.UseHttpsRedirection();

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

app.MapPut("/questions/{id:guid}", QuestionEndpointHandlers.UpdateQuestion);
app.MapDelete("/questions/{id:guid}", QuestionEndpointHandlers.DeleteQuestion);
app.MapPut("/api/questions/reorder", QuestionEndpointHandlers.ReorderQuestions);

app.MapGet("/sessions", SessionEndpointHandlers.GetSessions);
app.MapPost("/api/sessions", SessionEndpointHandlers.CreateSession);
app.MapGet("/api/sessions/join/{joinCode}", SessionEndpointHandlers.JoinSessionByCode);
app.MapGet("/sessions/{id:guid}/qr", SessionEndpointHandlers.GetSessionQr);

app.MapDefaultEndpoints();

app.Run();

public record CreateSessionRequest(string Title);
public record CreateSessionResponse(Guid Id, string JoinCode, string InstructorCode);

public partial class Program { }
