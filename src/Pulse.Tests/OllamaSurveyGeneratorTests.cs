using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.WebApi.Features.SurveyGeneration;

namespace Pulse.Tests;

public class OllamaSurveyGeneratorTests
{
    [Fact]
    public async Task Generate_UsesLocalEmbeddingRetrievalAndRequestedGemmaModel()
    {
        var material = Material();
        var handler = new OllamaHandler();
        var generator = CreateGenerator(handler);

        var questions = await generator.GenerateAsync(material, "Biology", 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, questions.Count);
        Assert.Equal("gemma4:latest", handler.ChatModel);
        Assert.Equal("all-minilm:latest", handler.EmbeddingModel);
        Assert.Contains("Photosynthesis", handler.Sources);
        Assert.All(questions, question => Assert.Contains(question.CorrectAnswer, question.Options));
        Assert.Equal(1, handler.ChatRequests);
        Assert.True(handler.EmbeddingRequests >= 2);
    }

    [Theory]
    [InlineData("await Shell.Current.GoToAsync($\"details?itemId={itemId}&name={name}\");")]
    [InlineData("var path = @\"C:\\new\\tests\\file.txt\";\r\n// Preserve tabs:\tand Unicode: \u201cquote\u201d")]
    public async Task Generate_CopiesOriginalPassageInsteadOfModelWrittenQuote(string sourceText)
    {
        var material = Material();
        material.Documents[0].Text = sourceText;
        var handler = new OllamaHandler { Fault = "invented-quote" };

        var questions = await CreateGenerator(handler)
            .GenerateAsync(material, "Code", 2, TestContext.Current.CancellationToken);

        Assert.All(questions, question =>
        {
            Assert.Equal($"{material.Documents[0].Id:N}:1", question.SourceId);
            Assert.Equal(sourceText, question.SourceQuote);
        });
    }

    [Fact]
    public async Task Generate_ResolvesTheSelectedContextPassage()
    {
        var material = Material();
        var handler = new OllamaHandler { PassageIndex = 1 };

        var questions = await CreateGenerator(handler)
            .GenerateAsync(material, "Biology", 2, TestContext.Current.CancellationToken);

        Assert.All(questions, question =>
        {
            Assert.Equal("context:1", question.SourceId);
            Assert.Equal(material.AdditionalContext, question.SourceQuote);
        });
    }

    [Fact]
    public async Task Generate_RejectsPassageFromMaterialOutsideRetrievedSelection()
    {
        var material = Material();
        material.Documents[0].Text = new string('x', 7000);
        var handler = new OllamaHandler { PassageIdOverride = $"{material.Documents[0].Id:N}:14" };

        await Assert.ThrowsAsync<LocalInferenceException>(() => CreateGenerator(handler)
            .GenerateAsync(material, "Biology", 2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://localhost:11434/v1/")]
    public async Task Generate_RegisteredClientPreservesV1BasePath(string? baseUrl)
    {
        var handler = new OllamaHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ollama:BaseUrl"] = baseUrl }).Build());
        services.AddLogging();
        services.AddPulseWebApiCoreServices("Filename=:memory:");
        services.AddHttpClient<OllamaSurveyGenerator>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var questions = await provider.GetRequiredService<OllamaSurveyGenerator>()
            .GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, questions.Count);
        Assert.Equal(1, handler.ChatRequests);
        Assert.Equal(2, handler.EmbeddingRequests);
    }

    [Theory]
    [InlineData("wrong-count")]
    [InlineData("duplicate")]
    [InlineData("bad-choice")]
    [InlineData("invented-passage")]
    [InlineData("missing-passage")]
    [InlineData("empty-passage")]
    [InlineData("null-passage")]
    [InlineData("null-text")]
    [InlineData("invalid-json")]
    [InlineData("incomplete")]
    [InlineData("invalid-vectors")]
    [InlineData("null-vector")]
    [InlineData("duplicate-index")]
    [InlineData("missing-choice")]
    [InlineData("empty-content")]
    public async Task Generate_RejectsInvalidModelOutput(string fault)
    {
        var generator = CreateGenerator(new OllamaHandler { Fault = fault });
        await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("RequestJson", "\"model\":\"gemma4:latest\"")]
    [InlineData("ResponseJson", "\"finish_reason\":\"stop\"")]
    [InlineData("QuestionsJson", "\"text\":\"Biology question 1?\"")]
    public async Task Generate_LogsJsonAtDebug(string property, string expected)
    {
        var logger = new RecordingLogger();
        var generator = CreateGenerator(new OllamaHandler(), logger);

        await generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Debug &&
            entry.State.Any(value => value.Key == property && value.Value is string json &&
                json.Contains(expected, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("wrong-count", "Expected 2 distinct questions; received 1 questions with 1 distinct texts.")]
    [InlineData("duplicate", "Expected 2 distinct questions; received 2 questions with 1 distinct texts.")]
    [InlineData("bad-choice", "Question at index 0: Correct answer does not exactly match an option.")]
    [InlineData("invented-passage", "Question at index 0: Passage ID does not match a selected grounding passage.")]
    [InlineData("missing-passage", "Question at index 0: Passage ID does not match a selected grounding passage.")]
    [InlineData("empty-passage", "Question at index 0: Passage ID does not match a selected grounding passage.")]
    [InlineData("null-passage", "Question at index 0: Passage ID does not match a selected grounding passage.")]
    [InlineData("null-text", "Question at index 0: Question text is empty.")]
    [InlineData("invalid-json", "Chat response or question content could not be deserialized.")]
    [InlineData("incomplete", "Chat response is missing or finish_reason is not stop.")]
    [InlineData("invalid-vectors", "An embedding vector is empty.")]
    [InlineData("null-vector", "An embedding vector is null.")]
    public async Task Generate_LogsSpecificFailureReason(string fault, string reason)
    {
        var logger = new RecordingLogger();
        var generator = CreateGenerator(new OllamaHandler { Fault = fault }, logger);

        await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning &&
            entry.State.Any(value => value.Key == "Reason" && Equals(value.Value, reason)));
    }

    [Fact]
    public async Task Generate_LogsJsonExceptionDetails()
    {
        var logger = new RecordingLogger();
        var generator = CreateGenerator(new OllamaHandler { Fault = "invalid-json" }, logger);

        await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Exception is JsonException);
    }

    [Fact]
    public async Task Generate_DoesNotLogPayloadsWhenDebugIsDisabled()
    {
        var logger = new RecordingLogger { MinimumLevel = LogLevel.Warning };
        var generator = CreateGenerator(new OllamaHandler(), logger);

        await generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Generate_LogsHttpFailureReason()
    {
        var logger = new RecordingLogger();
        var generator = CreateGenerator(new OllamaHandler { Status = HttpStatusCode.NotFound }, logger);

        await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning &&
            entry.State.Any(value => value.Key == "Reason" &&
                Equals(value.Value, "Request to embeddings for model all-minilm:latest returned HTTP 404.")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Generate_RejectsInvalidCountWithoutCallingOllama(int count)
    {
        var handler = new OllamaHandler();
        await Assert.ThrowsAsync<GroundingValidationException>(() =>
            CreateGenerator(handler).GenerateAsync(Material(), "Biology", count, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.EmbeddingRequests);
        Assert.Equal(0, handler.ChatRequests);
    }

    [Fact]
    public async Task Generate_MissingMaterialDoesNotCallOllama()
    {
        var handler = new OllamaHandler();
        await Assert.ThrowsAsync<GroundingValidationException>(() =>
            CreateGenerator(handler).GenerateAsync(new SurveyGrounding(), "Biology", 2, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.EmbeddingRequests);
    }

    [Fact]
    public async Task Generate_MissingModelReportsExactPullCommand()
    {
        var generator = CreateGenerator(new OllamaHandler { Status = HttpStatusCode.NotFound });
        var error = await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));
        Assert.Contains("ollama pull all-minilm:latest", error.Message);
    }

    [Fact]
    public void Chunking_PreservesMaterialWithBoundedOverlappingPassages()
    {
        var material = Material();
        material.Documents[0].Text = new string('x', 1300);
        var chunks = OllamaSurveyGenerator.ChunkMaterial(material);
        var documentChunks = chunks.Where(chunk => chunk.DocumentId != "context").ToList();
        Assert.Equal([600, 600, 300], documentChunks.Select(chunk => chunk.Text.Length));
        Assert.Equal(4, chunks.Count);
        Assert.Equal("context:1", chunks[^1].Id);
    }

    [Fact]
    public async Task LocalOllamaSmoke_GemmaGeneratesCitedQuestions()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PULSE_OLLAMA_SMOKE") == "1",
            "Opt-in smoke test requires local Ollama, gemma4:latest, and all-minilm:latest.");
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434/v1/"), Timeout = TimeSpan.FromMinutes(5) };
        var generator = new OllamaSurveyGenerator(http, new ConfigurationBuilder().Build(), NullLogger<OllamaSurveyGenerator>.Instance);
        var questions = await generator.GenerateAsync(Material(), "Photosynthesis", 2, TestContext.Current.CancellationToken);
        Assert.Equal(2, questions.Count);
        Assert.All(questions, question =>
        {
            Assert.Equal(4, question.Options.Count);
            Assert.Contains(question.CorrectAnswer, question.Options);
            Assert.NotEmpty(question.SourceQuote);
        });
    }

    internal static SurveyGrounding Material() => new()
    {
        Id = Guid.NewGuid(),
        AdditionalContext = "Focus on photosynthesis and chlorophyll.",
        Documents =
        [
            new SurveyGroundingDocument
            {
                FileName = "biology.txt",
                Text = "Photosynthesis converts light energy into chemical energy. Chlorophyll absorbs light in chloroplasts. " +
                    "Plants use carbon dioxide and water to produce glucose and oxygen. Oxygen is released as a byproduct."
            }
        ]
    };

    internal static OllamaSurveyGenerator CreateGenerator(
        HttpMessageHandler handler, ILogger<OllamaSurveyGenerator>? logger = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/v1/") },
            new ConfigurationBuilder().Build(), logger ?? NullLogger<OllamaSurveyGenerator>.Instance);

    private sealed class RecordingLogger : ILogger<OllamaSurveyGenerator>
    {
        public LogLevel MinimumLevel { get; init; } = LogLevel.Debug;
        public List<(LogLevel Level, KeyValuePair<string, object?>[] State, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                Entries.Add((logLevel, ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToArray(), exception));
        }
    }

    internal sealed class OllamaHandler : HttpMessageHandler
    {
        public string? Fault { get; init; }
        public int PassageIndex { get; init; }
        public string? PassageIdOverride { get; init; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? ChatModel { get; private set; }
        public string? EmbeddingModel { get; private set; }
        public string Sources { get; private set; } = string.Empty;
        public int ChatRequests { get; private set; }
        public int EmbeddingRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("localhost", request.RequestUri!.Host);
            Assert.Equal(11434, request.RequestUri.Port);
            if (Status != HttpStatusCode.OK)
                return new HttpResponseMessage(Status);
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Contains(request.RequestUri.AbsolutePath, new[] { "/v1/embeddings", "/v1/chat/completions" });
            if (request.RequestUri.AbsolutePath == "/v1/embeddings")
            {
                EmbeddingRequests++;
                EmbeddingModel = body.GetProperty("model").GetString();
                Assert.Equal("float", body.GetProperty("encoding_format").GetString());
                var count = body.GetProperty("input").GetArrayLength();
                return Json(new { data = Enumerable.Range(0, count).Select(index => new
                {
                    index = Fault == "duplicate-index" ? 0 : index,
                    embedding = Fault == "null-vector" ? null :
                        Fault == "invalid-vectors" ? Array.Empty<double>() : new[] { 1d, 0d }
                }).Reverse().ToArray() });
            }
            Assert.Equal("/v1/chat/completions", request.RequestUri.AbsolutePath);
            ChatRequests++;
            ChatModel = body.GetProperty("model").GetString();
            Assert.False(body.GetProperty("stream").GetBoolean());
            var format = body.GetProperty("response_format");
            Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
            Assert.Equal(JsonValueKind.Object, format.GetProperty("json_schema").GetProperty("schema").ValueKind);
            var questionSchema = format.GetProperty("json_schema").GetProperty("schema")
                .GetProperty("properties").GetProperty("questions").GetProperty("items");
            Assert.Contains("passageId", questionSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
            Assert.False(questionSchema.GetProperty("properties").TryGetProperty("sourceQuote", out _));
            Assert.False(questionSchema.GetProperty("properties").TryGetProperty("sourceId", out _));
            Assert.Equal(0.2, body.GetProperty("temperature").GetDouble());
            Assert.True(body.GetProperty("max_tokens").GetInt32() > 0);
            Assert.False(body.TryGetProperty("options", out _));
            var prompt = body.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Sources = prompt[(prompt.IndexOf("Sources: ", StringComparison.Ordinal) + 9)..];
            using var sources = JsonDocument.Parse(Sources);
            var source = sources.RootElement[PassageIndex];
            var questions = Enumerable.Range(1, 2).Select(index => new Dictionary<string, object?>
            {
                ["text"] = $"Biology question {index}?",
                ["options"] = new[] { "Light", "Sound", "Heat", "Gravity" },
                ["correctAnswer"] = "Light",
                ["explanation"] = "The passage describes light energy.",
                ["passageId"] = PassageIdOverride ?? source.GetProperty("passageId").GetString()
            }).ToList();
            switch (Fault)
            {
                case "wrong-count": questions.RemoveAt(1); break;
                case "duplicate": questions[1]["text"] = questions[0]["text"]; break;
                case "bad-choice": questions[0]["correctAnswer"] = "Not a choice"; break;
                case "invented-passage": questions[0]["passageId"] = "other-survey:1"; break;
                case "missing-passage": questions[0].Remove("passageId"); break;
                case "empty-passage": questions[0]["passageId"] = ""; break;
                case "null-passage": questions[0]["passageId"] = null; break;
                case "invented-quote": questions[0]["sourceQuote"] = "Invented text"; break;
                case "null-text": questions[0]["text"] = null; break;
            }
            return Json(new
            {
                choices = Fault == "missing-choice" ? [] : new[]
                {
                    new
                    {
                        finish_reason = Fault == "incomplete" ? "length" : "stop",
                        message = new { content = Fault == "empty-content" ? "" :
                            Fault == "invalid-json" ? "not json" : JsonSerializer.Serialize(new { questions }) }
                    }
                }
            });
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
