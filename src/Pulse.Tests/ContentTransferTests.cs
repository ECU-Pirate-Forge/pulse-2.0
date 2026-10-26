using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Pulse.Tests.Tests;

public sealed class ContentTransferTests : IDisposable
{
    private readonly WebApplicationFactory<ApiAssemblyMarker> factory;
    private readonly HttpClient client;
    private readonly LiteDatabase database;

    public ContentTransferTests()
    {
        factory = new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:pulse-db"] = "Filename=:memory:",
                    ["Security:InstructorCode"] = "INST"
                })))
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<LiteDatabase>();
                services.AddSingleton(_ => new LiteDatabase(new MemoryStream()));
            }));
        client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        database = factory.Services.GetRequiredService<LiteDatabase>();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private static PortableQuestion Question(string text = "Pick one") => new()
    {
        Text = text, Type = "MultipleChoice", Options = ["A", "B"], CorrectAnswers = ["B"],
        Explanation = "Because B", SourceId = "notes", SourceQuote = "Reference"
    };

    private static ContentTransferDocument Questions(params PortableQuestion[] questions) => new()
    {
        Format = "pulse", Version = 1, Kind = "questions", Questions = questions.ToList()
    };

    private Task<HttpResponseMessage> Import(ContentTransferDocument document, string? path = null) =>
        ImportJson(JsonSerializer.Serialize(document, ContentTransferJson.Options), path ?? document.Kind);

    private Task<HttpResponseMessage> ImportJson(string json, string path = "questions") =>
        client.PostAsync($"/api/content/import/{path}", new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private Session CreateSession(string owner = "INST", SessionStatus status = SessionStatus.Draft)
    {
        return factory.Services.GetRequiredService<ISessionRepository>().Insert(new Session
        {
            Title = "Original", InstructorCode = owner, JoinCode = "ABC123", Status = status,
            ResultsVisibility = ResultsVisibility.Revealed
        });
    }

    [Fact]
    public async Task DocumentedExample_ImportsAllSupportedAuthoringTypes()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "ContentTransfer", "questions.json"), TestContext.Current.CancellationToken);
        using var schema = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "ContentTransfer", "questions.schema.json"), TestContext.Current.CancellationToken));
        Assert.Equal(1, schema.RootElement.GetProperty("properties").GetProperty("version").GetProperty("const").GetInt32());

        using var response = await ImportJson(json);
        response.EnsureSuccessStatusCode();
        Assert.Equal(4, factory.Services.GetRequiredService<IQuestionBankRepository>().GetAll().Count());
    }

    [Fact]
    public async Task Survey_RoundTripPreservesAllQuestionTypesAndOrder_ButNotLiveState()
    {
        var original = CreateSession(status: SessionStatus.Active);
        var repository = factory.Services.GetRequiredService<QuestionRepository>();
        var types = new[]
        {
            QuestionType.MultipleChoice, QuestionType.MultipleAnswer, QuestionType.TrueFalse,
            QuestionType.Matching, QuestionType.LegacyLikertScale, QuestionType.LegacyOpenEnded
        };
        foreach (var (type, index) in types.Select((type, index) => (type, index)))
            repository.Insert(new Question
            {
                SessionId = original.Id, Text = type.ToString(), Type = type, SortOrder = 10 - index,
                Options = ["A", "B"], CorrectAnswers = type == QuestionType.Matching ? [] : ["B"],
                MatchingPairs = type == QuestionType.Matching
                    ? [new() { Prompt = "One", Match = "1" }, new() { Prompt = "Two", Match = "2" }] : [],
                Explanation = "An explanation", SourceId = "chapter-1", SourceQuote = "Source text"
            });
        database.GetCollection<SurveySubmission>("survey_submissions").Insert(
            new SurveySubmission { SessionId = original.Id, DeviceId = "private-participant" });

        using var exported = await client.GetAsync($"/api/content/surveys/{original.Id}", TestContext.Current.CancellationToken);
        exported.EnsureSuccessStatusCode();
        Assert.Equal("application/json", exported.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".json", exported.Content.Headers.ContentDisposition!.FileNameStar);
        var json = await exported.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("instructorCode", json);
        Assert.DoesNotContain("joinCode", json);
        Assert.DoesNotContain("private-participant", json);
        Assert.DoesNotContain("submittedAt", json);
        using var imported = await ImportJson(json, "survey");
        imported.EnsureSuccessStatusCode();
        var result = (await imported.Content.ReadFromJsonAsync<ContentImportResponse>(TestContext.Current.CancellationToken))!;
        Assert.Equal(types.Length, result.ImportedCount);
        var copy = factory.Services.GetRequiredService<ISessionRepository>().GetById(result.Id!.Value)!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.NotEqual(original.JoinCode, copy.JoinCode);
        Assert.Equal(SessionStatus.Draft, copy.Status);
        Assert.Equal(ResultsVisibility.Blinded, copy.ResultsVisibility);
        Assert.Equal("INST", copy.InstructorCode);
        Assert.Empty(database.GetCollection<SurveySubmission>("survey_submissions").Find(s => s.SessionId == copy.Id));
        var originalQuestions = repository.GetBySessionId(original.Id).OrderBy(q => q.SortOrder).ToList();
        var copiedQuestions = repository.GetBySessionId(copy.Id).OrderBy(q => q.SortOrder).ToList();
        Assert.Equal(originalQuestions.Select(q => q.Type), copiedQuestions.Select(q => q.Type));
        Assert.DoesNotContain(copiedQuestions, q => originalQuestions.Any(old => old.Id == q.Id));
        using var reexported = await client.GetAsync($"/api/content/surveys/{copy.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(json, await reexported.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Form_RoundTripPreservesDefinitionAndRegeneratesFieldIds()
    {
        var forms = factory.Services.GetRequiredService<FormRepository>();
        var original = forms.Insert(new Form
        {
            Title = "Signup", Description = "## Markdown\n\nInstructions", InstructorCode = "INST",
            JoinCode = "ABC123", Status = FormStatus.Open,
            Fields = Enum.GetValues<FormFieldType>().Select((type, i) => new FormField
            {
                Label = $"Field {i}", FieldType = type, Order = i, Required = true, Placeholder = "Enter value",
                Options = type == FormFieldType.Dropdown ? ["First", "Second"] : []
            }).ToList()
        });
        forms.InsertSubmission(new FormSubmission { FormId = original.Id, Values = new() { ["private"] = "secret" } });
        var json = await client.GetStringAsync($"/api/content/forms/{original.Id}", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("instructorCode", json);
        using var response = await ImportJson(json, "form");
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ContentImportResponse>(TestContext.Current.CancellationToken))!;
        var copy = forms.Get(result.Id!.Value)!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.NotEqual(original.JoinCode, copy.JoinCode);
        Assert.Equal(FormStatus.Draft, copy.Status);
        Assert.Equal(original.Description, copy.Description);
        Assert.DoesNotContain(copy.Fields, f => original.Fields.Any(old => old.Id == f.Id));
        Assert.Empty(forms.GetSubmissions(copy.Id));
        Assert.Equal(json, await client.GetStringAsync($"/api/content/forms/{copy.Id}", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bank_RoundTripPreservesMetadataAndCreatesCopies()
    {
        var bank = factory.Services.GetRequiredService<IQuestionBankRepository>();
        var original = bank.Insert(new QuestionBankItem
        {
            Text = "Bank question", Type = QuestionType.MultipleAnswer, Options = ["A", "B", "C"],
            CorrectAnswers = ["A", "C"], TriviaEligible = true, Course = "CSCI", Topic = "Networking",
            Tags = ["tag1", "tag2"], ModerationStatus = QuestionModerationStatus.Pending,
            Explanation = "Explanation", SourceId = "source", SourceQuote = "A quote"
        });
        var json = await client.GetStringAsync($"/api/content/questions?questionId={original.Id}", TestContext.Current.CancellationToken);
        using var response = await ImportJson(json);
        response.EnsureSuccessStatusCode();
        var copy = Assert.Single(bank.GetAll(), q => q.Id != original.Id);
        Assert.Equal(original.ModerationStatus, copy.ModerationStatus);
        Assert.Equal(original.Tags, copy.Tags);
        Assert.Equal(original.CorrectAnswers, copy.CorrectAnswers);
        Assert.Equal(json, await client.GetStringAsync($"/api/content/questions?questionId={copy.Id}", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QuestionImport_AppendsToDraftWithoutReplacingExistingQuestions()
    {
        var session = CreateSession();
        var questions = factory.Services.GetRequiredService<QuestionRepository>();
        var existing = questions.Insert(new Question { SessionId = session.Id, Text = "Existing", SortOrder = 5 });
        using var response = await Import(Questions(Question("First"), Question("Second")), $"questions?sessionId={session.Id}");
        response.EnsureSuccessStatusCode();
        var saved = questions.GetBySessionId(session.Id).OrderBy(q => q.SortOrder).ToList();
        Assert.Equal(new[] { "Existing", "First", "Second" }, saved.Select(q => q.Text));
        Assert.Equal(new[] { 5, 6, 7 }, saved.Select(q => q.SortOrder));
        Assert.Equal(existing.Id, saved[0].Id);
        Assert.Equal("Because B", saved[1].Explanation);
        var json = await client.GetStringAsync($"/api/content/questions?sessionId={session.Id}&questionId={saved[1].Id}",
            TestContext.Current.CancellationToken);
        Assert.Single(JsonSerializer.Deserialize<ContentTransferDocument>(json, ContentTransferJson.Options)!.Questions!);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"format":"pulse","version":2,"kind":"questions","questions":[]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":null}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[null]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[{"text":"Bad","type":"Invalid"}]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[{"text":"Bad","type":0}]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[{"text":"Bad","type":"TrueFalse","tags":null}]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[{"text":"Bad","type":"Matching","matchingPairs":[null]}]}""")]
    [InlineData("""{"format":"pulse","version":1,"kind":"questions","questions":[],"instructorCode":"OTHER"}""")]
    public async Task InvalidJson_Returns400WithoutSaving(string json)
    {
        using var response = await ImportJson(json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<IQuestionBankRepository>().GetAll());
    }

    [Fact]
    public async Task InvalidQuestionInBatch_DoesNotPartiallySave()
    {
        using var response = await Import(Questions(Question(), new PortableQuestion { Text = "", Type = "TrueFalse" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Question 2", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(factory.Services.GetRequiredService<IQuestionBankRepository>().GetAll());
    }

    [Theory]
    [InlineData("form", """{"format":"pulse","version":1,"kind":"form","title":"Bad","fields":[null]}""")]
    [InlineData("form", """{"format":"pulse","version":1,"kind":"form","title":"Bad","fields":[{"label":"x","fieldType":"Dropdown"}]}""")]
    [InlineData("form", """{"format":"pulse","version":1,"kind":"form","title":"Bad","fields":[{"label":"x","fieldType":"Unknown"}]}""")]
    [InlineData("survey", """{"format":"pulse","version":1,"kind":"survey","questions":[]}""")]
    [InlineData("form", """{"format":"pulse","version":1,"kind":"questions","questions":[]}""")]
    public async Task InvalidDefinitions_AreRejected(string kind, string json)
    {
        using var response = await ImportJson(json, kind);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<FormRepository>().GetByInstructorCode("INST"));
        Assert.Empty(await factory.Services.GetRequiredService<ISessionRepository>().GetByInstructorCodeAsync("INST"));
    }

    [Theory]
    [InlineData(SessionStatus.Active)]
    [InlineData(SessionStatus.Closed)]
    public async Task QuestionImport_RejectsNonDraftSurvey(SessionStatus status)
    {
        var session = CreateSession(status: status);
        using var response = await Import(Questions(Question()), $"questions?sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<QuestionRepository>().GetBySessionId(session.Id));
    }

    [Fact]
    public async Task Ownership_IsRequiredForSurveyAndFormExportAndQuestionImport()
    {
        var session = CreateSession("OTHER");
        var form = factory.Services.GetRequiredService<FormRepository>().Insert(
            new Form { Title = "Private", InstructorCode = "OTHER", JoinCode = "OTHER1" });
        foreach (var url in new[]
        {
            $"/api/content/surveys/{session.Id}", $"/api/content/questions?sessionId={session.Id}",
            $"/api/content/forms/{form.Id}"
        })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode);
        using var response = await Import(Questions(Question()), $"questions?sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("WRONG", HttpStatusCode.Forbidden)]
    public async Task AllTransferRoutes_RequireValidInstructorCode(string? code, HttpStatusCode expected)
    {
        client.DefaultRequestHeaders.Remove("InstructorCode");
        if (code is not null)
            client.DefaultRequestHeaders.Add("InstructorCode", code);
        foreach (var url in new[]
        {
            "/api/content/questions", $"/api/content/surveys/{Guid.NewGuid()}", $"/api/content/forms/{Guid.NewGuid()}"
        })
            Assert.Equal(expected, (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode);
        foreach (var kind in new[] { "survey", "form", "questions" })
            Assert.Equal(expected, (await Import(Questions(Question()), kind)).StatusCode);
    }

    [Fact]
    public async Task OversizedFile_IsRejected()
    {
        using var response = await ImportJson(new string(' ', ContentTransferJson.MaxFileBytes + 1));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task PersistenceFailure_RollsBackEntireBatch()
    {
        var bank = new Mock<IQuestionBankRepository>();
        var realBank = factory.Services.GetRequiredService<IQuestionBankRepository>();
        var count = 0;
        bank.Setup(b => b.Insert(It.IsAny<QuestionBankItem>())).Returns((QuestionBankItem item) =>
        {
            if (++count == 2)
                throw new InvalidOperationException("Simulated write failure");
            return realBank.Insert(item);
        });
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            Questions(Question("First"), Question("Second")), ContentTransferJson.Options));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ContentTransferEndpoints.Import(
            "questions", context, database, factory.Services.GetRequiredService<ISessionRepository>(),
            factory.Services.GetRequiredService<QuestionRepository>(), bank.Object,
            factory.Services.GetRequiredService<FormRepository>(), new JoinCodeGenerator(), new QuestionService()));
        Assert.Empty(realBank.GetAll());
    }
}
