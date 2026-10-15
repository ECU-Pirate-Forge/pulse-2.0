using Microsoft.AspNetCore.Http.Features;
using Pulse.Common.Services;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi.Features.SurveyGeneration;

public static class SurveyGenerationEndpoints
{
    public static void MapSurveyGeneration(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (GroundingConflictException ex) { return Results.Conflict(ex.Message); }
        });
        group.MapGet("/grounding", GetGrounding);
        group.MapPut("/grounding/context", SaveContext);
        group.MapPost("/grounding/documents", UploadDocuments);
        group.MapDelete("/grounding/documents/{documentId:guid}", RemoveDocument);
        group.MapPost("/questions/generate", Generate);
        group.MapPost("/questions/generated/{previewId:guid}/accept", Accept);
        group.MapGet("/questions/instructor", GetInstructorQuestions);
    }

    private static async Task<IResult?> CheckAccess(
        Guid sessionId, HttpContext context, ISessionRepository sessions, bool requireDraft = true)
    {
        var code = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
        if (string.IsNullOrWhiteSpace(code))
            return Results.Unauthorized();
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound("Survey not found.");
        if (!string.Equals(session.InstructorCode, code, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return requireDraft && session.Status != SessionStatus.Draft
            ? Results.Conflict("Grounding and generated questions can only be changed while the survey is in Draft.")
            : null;
    }

    private static async Task<IResult> GetGrounding(
        Guid sessionId, HttpContext context, ISessionRepository sessions, SurveyGenerationRepository repository)
    {
        var error = await CheckAccess(sessionId, context, sessions, false);
        return error ?? Results.Ok(Summary(repository.GetGrounding(sessionId)));
    }

    private static async Task<IResult> GetInstructorQuestions(
        Guid sessionId, HttpContext context, ISessionRepository sessions, QuestionRepository questions)
    {
        var error = await CheckAccess(sessionId, context, sessions, false);
        return error ?? Results.Ok(questions.GetBySessionId(sessionId).OrderBy(question => question.SortOrder));
    }

    private static async Task<IResult> SaveContext(
        Guid sessionId, SaveGroundingContextRequest request, HttpContext context,
        ISessionRepository sessions, SurveyGenerationRepository repository)
    {
        var error = await CheckAccess(sessionId, context, sessions);
        if (error is not null)
            return error;
        if (request.AdditionalContext is null || request.AdditionalContext.Length > DocumentTextExtractor.MaximumContextCharacters)
            return Results.BadRequest("Additional context may contain at most 10,000 characters.");
        var material = repository.GetGrounding(sessionId);
        if (material.AdditionalContext != request.AdditionalContext.Trim())
        {
            material.AdditionalContext = request.AdditionalContext.Trim();
            repository.SaveGrounding(material);
        }
        return Results.Ok(Summary(material));
    }

    private static async Task<IResult> UploadDocuments(
        Guid sessionId, HttpContext context, ISessionRepository sessions,
        SurveyGenerationRepository repository, DocumentTextExtractor extractor)
    {
        var error = await CheckAccess(sessionId, context, sessions);
        if (error is not null)
            return error;
        if (!context.Request.HasFormContentType)
            return Results.BadRequest("Upload documents using multipart/form-data.");
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = 26 * 1024 * 1024;
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var material = repository.GetGrounding(sessionId);
            if (form.Files.Count == 0 || material.Documents.Count + form.Files.Count > DocumentTextExtractor.MaximumFiles)
                return Results.BadRequest("Attach between one and five documents in total.");
            var documents = new List<SurveyGroundingDocument>();
            foreach (var file in form.Files)
            {
                if (file.Length is < 1 or > DocumentTextExtractor.MaximumFileBytes)
                    return Results.BadRequest("Each document must contain between 1 byte and 5 MB.");
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, context.RequestAborted);
                var bytes = buffer.ToArray();
                var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                    return Results.BadRequest("Document names must contain between 1 and 200 characters.");
                documents.Add(new SurveyGroundingDocument
                {
                    FileName = name, Content = bytes, Text = extractor.Extract(name, bytes)
                });
            }
            material.Documents.AddRange(documents);
            error = await CheckAccess(sessionId, context, sessions);
            if (error is not null)
                return error;
            return Results.Ok(Summary(repository.SaveGrounding(material)));
        }
        catch (GroundingValidationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest("The document upload exceeds the form limits or is malformed.");
        }
    }

    private static async Task<IResult> RemoveDocument(
        Guid sessionId, Guid documentId, HttpContext context,
        ISessionRepository sessions, SurveyGenerationRepository repository)
    {
        var error = await CheckAccess(sessionId, context, sessions);
        if (error is not null)
            return error;
        var material = repository.GetGrounding(sessionId);
        if (material.Documents.RemoveAll(document => document.Id == documentId) == 0)
            return Results.NotFound("Document not found.");
        return Results.Ok(Summary(repository.SaveGrounding(material)));
    }

    private static async Task<IResult> Generate(
        Guid sessionId, GenerateSurveyQuestionsRequest request, HttpContext context,
        ISessionRepository sessions, SurveyGenerationRepository repository, OllamaSurveyGenerator generator,
        ILoggerFactory loggerFactory)
    {
        var error = await CheckAccess(sessionId, context, sessions);
        if (error is not null)
            return error;
        var material = repository.GetGrounding(sessionId);
        try
        {
            var session = await sessions.GetByIdAsync(sessionId);
            var questions = await generator.GenerateAsync(material, session!.Title, request.Count, context.RequestAborted);
            error = await CheckAccess(sessionId, context, sessions);
            if (error is not null)
                return error;
            if (repository.GetGrounding(sessionId).Revision != material.Revision)
                return Results.Conflict("Grounding changed during generation. Generate a new preview.");
            var preview = repository.SavePreview(sessionId, material.Revision, questions);
            return Results.Ok(new GeneratedSurveyPreviewResponse(preview.Id, preview.Questions));
        }
        catch (GroundingValidationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is LocalInferenceException or HttpRequestException ||
                                   ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            loggerFactory.CreateLogger("SurveyGeneration")
                .LogWarning("Local generation failed SessionId={SessionId} FailureType={FailureType}", sessionId, ex.GetType().Name);
            var message = ex is LocalInferenceException
                ? ex.Message
                : ex is OperationCanceledException
                    ? "Local Ollama generation timed out. Try fewer questions."
                    : "Could not connect to local Ollama. Start Ollama at http://localhost:11434 and try again.";
            return Results.Problem(message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> Accept(
        Guid sessionId, Guid previewId, HttpContext context,
        ISessionRepository sessions, SurveyGenerationRepository repository)
    {
        var error = await CheckAccess(sessionId, context, sessions);
        if (error is not null)
            return error;
        try
        {
            return Results.Ok(repository.Accept(sessionId, previewId));
        }
        catch (GroundingValidationException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    private static SurveyGroundingResponse Summary(SurveyGrounding material) => new(
        material.Revision, material.AdditionalContext,
        material.Documents.Select(document => new GroundingDocumentResponse(
            document.Id, document.FileName, document.Text.Length)).ToList());
}
