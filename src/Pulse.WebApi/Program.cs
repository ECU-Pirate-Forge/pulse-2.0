using Pulse.Application.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;
using Pulse.Common.Services;
using Pulse.WebApi;
using Scalar.AspNetCore;
using Pulse.Shared.Contracts.V1;
using Pulse.WebApi.Features.SurveyGeneration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";

builder.Services.AddPulseWebApiCoreServices(connectionString);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

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
    return repo.GetAll().Select(StudentQuestionResponse.FromQuestion);
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

app.MapPut("/questions/{id:guid}", QuestionEndpointHandlers.UpdateQuestionForInstructor);
app.MapDelete("/questions/{id:guid}", QuestionEndpointHandlers.DeleteQuestionForInstructor);
app.MapPut("/api/questions/reorder", QuestionEndpointHandlers.ReorderQuestionsForInstructor);

// Session-specific question endpoints
app.MapGet("/api/sessions/{sessionId:guid}/questions", (Guid sessionId, QuestionRepository questionRepo) =>
{
    var questions = questionRepo.GetBySessionId(sessionId).OrderBy(q => q.SortOrder).ToList();
    return Results.Ok(questions.Select(StudentQuestionResponse.FromQuestion));
});

app.MapPost("/api/sessions/{sessionId:guid}/questions", QuestionEndpointHandlers.CreateQuestionForSession);
app.MapSurveyGeneration();

app.MapPost("/api/questionbank", QuestionBankEndpointHandlers.CreateQuestionBankItem);
app.MapGet("/api/questionbank", QuestionBankEndpointHandlers.GetQuestionBankItems);
app.MapPost("/api/questionbank/suggestions", QuestionBankEndpointHandlers.SubmitQuestionSuggestion);
app.MapPut("/api/questionbank/{id:guid}", QuestionBankEndpointHandlers.ModerateQuestionBankItem);
app.MapGet("/sessions", SessionEndpointHandlers.GetSessions);
app.MapPost("/api/sessions", SessionEndpointHandlers.CreateSession);
app.MapDelete("/api/sessions/{id:guid}", SessionEndpointHandlers.DeleteSession);
app.MapGet("/api/sessions/join/{joinCode}", SessionEndpointHandlers.JoinSessionByCode);
app.MapGet("/api/sessions/qr/{joinCode}", SessionEndpointHandlers.GetSessionQrByCode);
app.MapGet("/sessions/{id:guid}/qr", SessionEndpointHandlers.GetSessionQr);
app.MapPost("/api/sessions/{sessionId:guid}/submit", SurveySubmissionEndpointHandlers.SubmitSurvey);
app.MapGet("/api/sessions/{id:guid}/results", SessionResultsEndpointHandlers.GetSessionResults);
app.MapGet("/api/sessions/{id:guid}/results/student", SessionResultsEndpointHandlers.GetStudentResults);

app.MapPut("/api/sessions/{id:guid}/unblind", SessionEndpointHandlers.UnblindSession);
app.MapPut("/api/sessions/{id:guid}/activate", SessionEndpointHandlers.ActivateSession);
app.MapPut("/api/sessions/{id:guid}/close", SessionEndpointHandlers.CloseSession);
app.MapGet("/api/admin/export-db", AdminEndpointHandlers.ExportDb);
app.MapPost("/api/questionbank/import/preview", CsvPreviewEndpointHandlers.PreviewImport).DisableAntiforgery();
app.MapPost("/api/sessions/{sessionId:guid}/questions/import", QuestionBankImportEndpointHandlers.ImportQuestions);
app.MapHub<SessionHub>("/hubs/session");
app.MapGet("/api/trivia", TriviaEndpointHandlers.GetGames);
app.MapPost("/api/trivia", TriviaEndpointHandlers.CreateGame);
app.MapGet("/api/trivia/join/{joinCode}", TriviaEndpointHandlers.GetJoinInfo);
app.MapPost("/api/trivia/join/{joinCode}", TriviaEndpointHandlers.Join);
app.MapGet("/api/trivia/{gameId:guid}/state/{participantId:guid}", TriviaEndpointHandlers.GetState);
app.MapGet("/api/trivia/{gameId:guid}/control", TriviaEndpointHandlers.GetInstructorState);
app.MapPost("/api/trivia/{gameId:guid}/teams", TriviaEndpointHandlers.AddTeam);
app.MapPost("/api/trivia/{gameId:guid}/questions", TriviaEndpointHandlers.AddQuestion);
app.MapPut("/api/trivia/{gameId:guid}/questions/{questionId:guid}/open", TriviaEndpointHandlers.OpenQuestion);
app.MapPut("/api/trivia/{gameId:guid}/question/close", TriviaEndpointHandlers.CloseQuestion);
app.MapPut("/api/trivia/{gameId:guid}/finish", TriviaEndpointHandlers.FinishGame);
app.MapPost("/api/trivia/{gameId:guid}/questions/{questionId:guid}/answers", TriviaEndpointHandlers.SubmitAnswer);
app.MapDefaultEndpoints();

app.Run();

public record CreateSessionRequest(string Title);
public record CreateSessionResponse(Guid Id, string JoinCode, string InstructorCode);

public partial class Program { }
