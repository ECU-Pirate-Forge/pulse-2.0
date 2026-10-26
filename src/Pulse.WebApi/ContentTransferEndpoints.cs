using System.Text.Json;
using LiteDB;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Pulse.WebApi;

public static class ContentTransferEndpoints
{
    public static void MapContentTransfer(this WebApplication app)
    {
        app.MapGet("/api/content/surveys/{id:guid}", ExportSurvey);
        app.MapGet("/api/content/forms/{id:guid}", ExportForm);
        app.MapGet("/api/content/questions", ExportQuestions);
        app.MapPost("/api/content/import/{kind}", Import);
    }

    public static IResult ExportSurvey(Guid id, HttpContext context, ISessionRepository sessions, QuestionRepository questions)
    {
        var code = InstructorCode(context);
        if (code is null)
            return Results.Unauthorized();
        var session = sessions.GetById(id);
        if (session is null)
            return Results.NotFound();
        if (session.InstructorCode != code)
            return Results.StatusCode(403);
        if (session.Mode != SessionMode.Survey)
            return Results.BadRequest("Only surveys can be exported as survey definitions.");

        return Download(new ContentTransferDocument
        {
            Format = "pulse", Version = 1, Kind = "survey", Title = session.Title,
            Questions = questions.GetBySessionId(id).OrderBy(q => q.SortOrder).Select(FromQuestion).ToList()
        }, $"survey-{id}.json");
    }

    public static IResult ExportForm(Guid id, HttpContext context, FormRepository forms)
    {
        var code = InstructorCode(context);
        if (code is null)
            return Results.Unauthorized();
        var form = forms.Get(id);
        if (form is null)
            return Results.NotFound();
        if (form.InstructorCode != code)
            return Results.StatusCode(403);

        return Download(new ContentTransferDocument
        {
            Format = "pulse", Version = 1, Kind = "form", Title = form.Title, Description = form.Description,
            Fields = form.Fields.OrderBy(f => f.Order).Select(f => new PortableFormField
            {
                Label = f.Label, FieldType = f.FieldType.ToString(), Required = f.Required,
                Placeholder = f.Placeholder, Options = f.Options
            }).ToList()
        }, $"form-{id}.json");
    }

    public static IResult ExportQuestions(
        HttpContext context, ISessionRepository sessions, QuestionRepository questions,
        IQuestionBankRepository bank, Guid? sessionId = null, Guid? questionId = null)
    {
        var code = InstructorCode(context);
        if (code is null)
            return Results.Unauthorized();

        List<PortableQuestion> content;
        if (sessionId.HasValue)
        {
            var session = sessions.GetById(sessionId.Value);
            if (session is null)
                return Results.NotFound();
            if (session.InstructorCode != code)
                return Results.StatusCode(403);
            var selected = questions.GetBySessionId(sessionId.Value).OrderBy(q => q.SortOrder).ToList();
            if (questionId.HasValue)
                selected = selected.Where(q => q.Id == questionId.Value).ToList();
            content = selected.Select(FromQuestion).ToList();
        }
        else
        {
            var selected = questionId.HasValue
                ? bank.GetAll().Where(q => q.Id == questionId.Value)
                : bank.GetAll();
            content = selected.Select(q => new PortableQuestion
            {
                Text = q.Text, Type = q.Type.ToString(), Options = q.Options, MatchingPairs = q.MatchingPairs,
                CorrectAnswers = q.CorrectAnswers, Course = q.Course, Topic = q.Topic, Tags = q.Tags,
                TriviaEligible = q.TriviaEligible, IsDraft = q.IsDraft, ModerationStatus = q.ModerationStatus.ToString(),
                Explanation = q.Explanation, SourceId = q.SourceId, SourceQuote = q.SourceQuote
            }).ToList();
        }

        if (questionId.HasValue && content.Count == 0)
            return Results.NotFound();
        return Download(new ContentTransferDocument
        {
            Format = "pulse", Version = 1, Kind = "questions", Questions = content
        }, "questions.json");
    }

    public static async Task<IResult> Import(
        string kind, HttpContext context, LiteDatabase database, ISessionRepository sessions,
        QuestionRepository questions, IQuestionBankRepository bank, FormRepository forms,
        IJoinCodeGenerator joinCodes, QuestionService validation, Guid? sessionId = null)
    {
        var code = InstructorCode(context);
        if (code is null)
            return Results.Unauthorized();
        if (kind is not ("survey" or "form" or "questions"))
            return Results.BadRequest("Import kind must be survey, form, or questions.");
        if (sessionId.HasValue && kind != "questions")
            return Results.BadRequest("Only question files can be appended to an existing survey.");

        Session? target = null;
        if (sessionId.HasValue)
        {
            target = sessions.GetById(sessionId.Value);
            if (target is null)
                return Results.NotFound();
            if (target.InstructorCode != code)
                return Results.StatusCode(403);
            if (target.Mode != SessionMode.Survey || target.Status != SessionStatus.Draft)
                return Results.Conflict("Questions can only be imported into a draft survey.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) != 0)
        {
            if (buffer.Length + read > ContentTransferJson.MaxFileBytes)
                return Results.Problem("JSON files must be 2 MiB or smaller.", statusCode: 413);
            buffer.Write(chunk, 0, read);
        }

        ContentTransferDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ContentTransferDocument>(buffer.ToArray(), ContentTransferJson.Options);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest($"Invalid Pulse JSON at {ex.Path ?? "$"}: {ex.Message}");
        }

        var error = Validate(document, kind, validation, sessionId.HasValue);
        if (error is not null)
            return Results.BadRequest(error);
        // Validation above checks the complete document before any writes.
        return Persist(document!, target, code, database, questions, bank, forms, joinCodes);
    }

    private static string? Validate(ContentTransferDocument? document, string kind, QuestionService validation, bool forSurvey)
    {
        if (document is null || document.Format != "pulse" || document.Version != 1)
            return "Expected a Pulse JSON document with format 'pulse' and version 1.";
        if (document.Kind != kind)
            return $"Expected a '{kind}' file, not '{document.Kind}'.";
        if (kind is "survey" or "form" && string.IsNullOrWhiteSpace(document.Title))
            return "A title is required.";
        if (kind == "form")
        {
            if (document.Fields is null || document.Questions is not null)
                return "A form file requires fields and must not contain questions.";
            foreach (var field in document.Fields)
            {
                if (field is null || !Enum.GetNames<FormFieldType>().Contains(field.FieldType))
                    return "Each form field needs a valid fieldType.";
                if (field.Options is null || field.Options.Any(string.IsNullOrWhiteSpace))
                    return "Field options must be non-empty strings.";
            }
            return FormService.ValidateDefinition(FormRequest(document));
        }
        if (document.Questions is null || document.Fields is not null || document.Description is not null ||
            (kind == "questions" && document.Title is not null))
            return "Question and survey files require questions and must not contain form fields or unrelated properties.";

        for (var i = 0; i < document.Questions.Count; i++)
        {
            var q = document.Questions[i];
            var prefix = $"Question {i + 1}: ";
            if (q is null || string.IsNullOrWhiteSpace(q.Text))
                return prefix + "text is required.";
            if (q.IsDraft && (document.Kind == "survey" || forSurvey))
                return prefix + "draft questions must be reviewed and approved in the question bank before use.";
            if (!Enum.GetNames<QuestionType>().Contains(q.Type))
                return prefix + "type is invalid.";
            if (q.Options is null || q.MatchingPairs is null || q.CorrectAnswers is null || q.Tags is null ||
                q.Options.Any(string.IsNullOrWhiteSpace) || q.CorrectAnswers.Any(string.IsNullOrWhiteSpace) ||
                q.Tags.Any(string.IsNullOrWhiteSpace) ||
                q.MatchingPairs.Any(p => p is null || p.Prompt is null || p.Match is null ||
                    !q.IsDraft && (string.IsNullOrWhiteSpace(p.Prompt) || string.IsNullOrWhiteSpace(p.Match))) ||
                q.Explanation is null || q.SourceId is null || q.SourceQuote is null || q.Course is null || q.Topic is null)
                return prefix + "arrays and metadata must not be null; array entries must be complete, non-empty values.";
            if (!Enum.GetNames<QuestionModerationStatus>().Contains(q.ModerationStatus))
                return prefix + "moderationStatus is invalid.";
            var type = Enum.Parse<QuestionType>(q.Type);
            // Legacy definitions remain transferable, but cannot be authored through the editors.
            if (!q.IsDraft && type is not (QuestionType.LegacyLikertScale or QuestionType.LegacyOpenEnded))
            {
                var result = validation.ValidateQuestion(new QuestionDTO
                {
                    Type = type, Options = q.Options, MatchingPairs = q.MatchingPairs
                });
                if (!result.IsValid)
                    return prefix + result.ErrorMessage;
            }
            var triviaError = QuestionBankEndpointHandlers.ValidateTriviaConfiguration(
                type, q.TriviaEligible, q.Options, q.CorrectAnswers);
            if (!q.IsDraft && triviaError is not null)
                return prefix + triviaError;
        }
        return null;
    }

    private static IResult Persist(
        ContentTransferDocument document, Session? target, string code, LiteDatabase database,
        QuestionRepository questions, IQuestionBankRepository bank, FormRepository forms, IJoinCodeGenerator joinCodes)
    {
        if (!database.BeginTrans())
            throw new InvalidOperationException("Unable to start content import transaction.");
        try
        {
            Guid? id = target?.Id;
            var count = document.Questions?.Count ?? document.Fields!.Count;
            if (document.Kind == "form")
            {
                string joinCode;
                do { joinCode = joinCodes.Generate(); } while (forms.GetByJoinCode(joinCode) is not null);
                var form = new Form { InstructorCode = code, JoinCode = joinCode };
                FormService.ApplyDefinition(form, FormRequest(document));
                forms.Insert(form);
                id = form.Id;
            }
            else
            {
                if (document.Kind == "survey")
                {
                    var sessions = database.GetCollection<Session>("sessions");
                    string joinCode;
                    do { joinCode = joinCodes.Generate(); } while (sessions.Exists(s => s.JoinCode == joinCode));
                    target = new Session
                    {
                        Id = Guid.NewGuid(), Title = document.Title!, InstructorCode = code, JoinCode = joinCode,
                        Mode = SessionMode.Survey, Status = SessionStatus.Draft,
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    };
                    // Insert inside this transaction without publishing an uncommitted session to the repository cache.
                    sessions.Insert(target);
                    id = target.Id;
                }
                var sortOrder = target is null ? 0 : questions.GetBySessionId(target.Id)
                    .Select(q => q.SortOrder).DefaultIfEmpty(-1).Max() + 1;
                foreach (var q in document.Questions!)
                {
                    var type = Enum.Parse<QuestionType>(q.Type);
                    if (target is not null)
                    {
                        questions.Insert(new Question
                        {
                            SessionId = target.Id, Text = q.Text, Type = type, Options = q.Options,
                            MatchingPairs = q.MatchingPairs, CorrectAnswers = q.CorrectAnswers, Tags = q.Tags, SortOrder = sortOrder++,
                            Explanation = q.Explanation, SourceId = q.SourceId, SourceQuote = q.SourceQuote
                        });
                    }
                    else
                    {
                        bank.Insert(new QuestionBankItem
                        {
                            Text = q.Text, Type = type, Options = q.Options, MatchingPairs = q.MatchingPairs,
                            CorrectAnswers = q.CorrectAnswers, Course = q.Course, Topic = q.Topic, Tags = q.Tags,
                            Explanation = q.Explanation, SourceId = q.SourceId, SourceQuote = q.SourceQuote,
                            TriviaEligible = q.TriviaEligible, IsDraft = q.IsDraft, ModerationStatus = Enum.Parse<QuestionModerationStatus>(q.ModerationStatus),
                            Source = QuestionSource.InstructorAuthored, ContributorAttribution = "Instructor"
                        });
                    }
                }
            }
            if (!database.Commit())
                throw new InvalidOperationException("Unable to commit content import transaction.");
            return Results.Ok(new ContentImportResponse(document.Kind, id, count));
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    private static SaveFormRequest FormRequest(ContentTransferDocument document) =>
        new(document.Title!, document.Description, document.Fields!.Select(f => new FormFieldRequest(
            null, f.Label, Enum.Parse<FormFieldType>(f.FieldType), f.Required, f.Placeholder, f.Options)).ToList());

    private static PortableQuestion FromQuestion(Question q) => new()
    {
        Text = q.Text, Type = q.Type.ToString(), Options = q.Options, MatchingPairs = q.MatchingPairs,
        CorrectAnswers = q.CorrectAnswers, Tags = q.Tags, Explanation = q.Explanation, SourceId = q.SourceId, SourceQuote = q.SourceQuote
    };

    private static IResult Download(ContentTransferDocument document, string name)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ContentTransferJson.Options);
        return bytes.Length > ContentTransferJson.MaxFileBytes
            ? Results.Problem("This export exceeds the 2 MiB import limit. Export questions individually instead.", statusCode: 413)
            : Results.File(bytes, "application/json", name);
    }

    private static string? InstructorCode(HttpContext context) =>
        context.Items[InstructorCodeMiddleware.HeaderName] is string code && !string.IsNullOrWhiteSpace(code) ? code : null;
}
