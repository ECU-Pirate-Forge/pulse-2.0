using Pulse.Application.Services;
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

// Define seeding method
async Task SeedDemoSessions(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

    var demoSessions = new[]
    {
        new { Code = "BIO123", Title = "Biology Quiz - Chapter 5" },
        new { Code = "MATH45", Title = "Math Concepts Test" },
        new { Code = "HIST99", Title = "History Final Exam" }
    };

    foreach (var demo in demoSessions)
    {
        var existingSession = await repo.GetByJoinCodeAsync(demo.Code);
        if (existingSession == null)
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                Title = demo.Title,
                JoinCode = demo.Code,
                InstructorCode = "INSTRUCTOR001",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repo.InsertAsync(session);
        }
    }
}

// Seed demo sessions in development
if (app.Environment.IsDevelopment())
{
    await SeedDemoSessions(app.Services);
}

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

app.MapPost("/questions", QuestionEndpointHandlers.CreateQuestion);

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

app.MapPost("/api/questionbank", QuestionBankEndpointHandlers.CreateQuestionBankItem);
app.MapGet("/api/questionbank", QuestionBankEndpointHandlers.GetQuestionBankItems);
app.MapGet("/sessions", SessionEndpointHandlers.GetSessions);
app.MapPost("/api/sessions", SessionEndpointHandlers.CreateSession);
app.MapGet("/api/sessions/join/{joinCode}", SessionEndpointHandlers.JoinSessionByCode);
app.MapGet("/api/sessions/qr/{joinCode}", SessionEndpointHandlers.GetSessionQrByCode);
app.MapGet("/sessions/{id:guid}/qr", SessionEndpointHandlers.GetSessionQr);

app.MapGet("/api/sessions/{id:guid}/results", SessionResultsEndpointHandlers.GetSessionResults);
app.MapPut("/api/sessions/{id:guid}/unblind", SessionEndpointHandlers.UnblindSession);
app.MapGet("/api/admin/export-db", AdminEndpointHandlers.ExportDb);
app.MapPost("/api/sessions/{sessionId:guid}/questions/import", QuestionBankImportEndpointHandlers.ImportQuestions);
app.MapDefaultEndpoints();

app.Run();

public record CreateSessionRequest(string Title);
public record CreateSessionResponse(Guid Id, string JoinCode, string InstructorCode);

public partial class Program { }
