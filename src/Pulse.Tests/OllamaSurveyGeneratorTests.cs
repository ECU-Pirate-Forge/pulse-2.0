using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
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
    [InlineData("wrong-count")]
    [InlineData("duplicate")]
    [InlineData("bad-choice")]
    [InlineData("invented-source")]
    [InlineData("invented-quote")]
    [InlineData("null-text")]
    [InlineData("invalid-json")]
    [InlineData("incomplete")]
    [InlineData("invalid-vectors")]
    [InlineData("null-vector")]
    public async Task Generate_RejectsInvalidModelOutput(string fault)
    {
        var generator = CreateGenerator(new OllamaHandler { Fault = fault });
        await Assert.ThrowsAsync<LocalInferenceException>(() =>
            generator.GenerateAsync(Material(), "Biology", 2, TestContext.Current.CancellationToken));
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
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(5) };
        var generator = new OllamaSurveyGenerator(http, new ConfigurationBuilder().Build());
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

    internal static OllamaSurveyGenerator CreateGenerator(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") }, new ConfigurationBuilder().Build());

    internal sealed class OllamaHandler : HttpMessageHandler
    {
        public string? Fault { get; init; }
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
            if (request.RequestUri.AbsolutePath == "/api/embed")
            {
                EmbeddingRequests++;
                EmbeddingModel = body.GetProperty("model").GetString();
                var count = body.GetProperty("input").GetArrayLength();
                if (Fault == "null-vector")
                    return Json(new { embeddings = Enumerable.Repeat<double[]?>(null, count).ToArray() });
                return Json(new { embeddings = Enumerable.Range(0, count)
                    .Select(_ => Fault == "invalid-vectors" ? Array.Empty<double>() : new[] { 1d, 0d }).ToArray() });
            }
            Assert.Equal("/api/chat", request.RequestUri.AbsolutePath);
            ChatRequests++;
            ChatModel = body.GetProperty("model").GetString();
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.False(body.GetProperty("think").GetBoolean());
            Assert.Equal(JsonValueKind.Object, body.GetProperty("format").ValueKind);
            var prompt = body.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Sources = prompt[(prompt.IndexOf("Sources: ", StringComparison.Ordinal) + 9)..];
            using var sources = JsonDocument.Parse(Sources);
            var source = sources.RootElement[0];
            var quote = source.GetProperty("text").GetString()!;
            var questions = Enumerable.Range(1, 2).Select(index => new GeneratedSurveyQuestion
            {
                Text = $"Biology question {index}?",
                Options = ["Light", "Sound", "Heat", "Gravity"],
                CorrectAnswer = "Light", Explanation = "The passage describes light energy.",
                SourceId = source.GetProperty("sourceId").GetString()!, SourceQuote = quote
            }).ToList();
            switch (Fault)
            {
                case "wrong-count": questions.RemoveAt(1); break;
                case "duplicate": questions[1].Text = questions[0].Text; break;
                case "bad-choice": questions[0].CorrectAnswer = "Not a choice"; break;
                case "invented-source": questions[0].SourceId = "other-survey:1"; break;
                case "invented-quote": questions[0].SourceQuote = "Invented text"; break;
                case "null-text": questions[0].Text = null!; break;
            }
            return Json(new
            {
                done = Fault != "incomplete",
                message = new { content = Fault == "invalid-json" ? "not json" : JsonSerializer.Serialize(new { questions }) }
            });
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
