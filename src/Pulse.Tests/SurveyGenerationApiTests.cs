using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LiteDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pulse.WebApi.Features.SurveyGeneration;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests;

public class SurveyGenerationApiTests
{
    [Fact]
    public async Task Grounding_UploadAndContext_PersistForDraftSurvey()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Photosynthesis converts light into chemical energy.")),
            "files", "biology.txt");
        var response = await client.PostAsync($"/api/sessions/{sessionId}/grounding/documents", upload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response = await client.PutAsJsonAsync($"/api/sessions/{sessionId}/grounding/context",
            new { additionalContext = "Focus on photosynthesis." });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var material = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sessionId}/grounding");
        Assert.Equal("Focus on photosynthesis.", material.GetProperty("additionalContext").GetString());
        Assert.Equal("biology.txt", material.GetProperty("documents")[0].GetProperty("fileName").GetString());
    }

    [Theory]
    [InlineData("GET", "grounding")]
    [InlineData("POST", "grounding/documents")]
    [InlineData("PUT", "grounding/context")]
    [InlineData("POST", "questions/generate")]
    [InlineData("POST", "questions/generated/00000000-0000-0000-0000-000000000001/accept")]
    [InlineData("GET", "questions/instructor")]
    public async Task GenerationRoutes_RequireInstructorCode(string method, string suffix)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/sessions/{Guid.NewGuid()}/{suffix}");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StudentQuestionApi_DoesNotExposeAnswerKeys()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        using var response = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/questions", new
        {
            text = "Which is a planet?", type = 0, options = new[] { "Mars", "Sun" },
            correctAnswers = new[] { "Mars" }, explanation = "Mars is a planet."
        });
        response.EnsureSuccessStatusCode();
        var instructorQuestions = await client.GetStringAsync($"/api/sessions/{sessionId}/questions/instructor");
        Assert.Contains("correctAnswers", instructorQuestions);
        client.DefaultRequestHeaders.Remove("InstructorCode");
        var studentQuestions = await client.GetStringAsync($"/api/sessions/{sessionId}/questions");
        Assert.DoesNotContain("correctAnswers", studentQuestions);
        Assert.DoesNotContain("explanation", studentQuestions);
    }

    [Fact]
    public async Task Generation_PreviewThenApprovePersistsAnswerKeysExactlyOnce()
    {
        var handler = new OllamaSurveyGeneratorTests.OllamaHandler();
        using var factory = CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<OllamaSurveyGenerator>();
            services.AddSingleton(OllamaSurveyGeneratorTests.CreateGenerator(handler));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        (await client.PutAsJsonAsync($"/api/sessions/{sessionId}/grounding/context",
            new { additionalContext = "Photosynthesis converts light energy to chemical energy." })).EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/questions/generate", new { count = 2 });
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = preview.GetProperty("id").GetGuid();
        Assert.Equal(2, preview.GetProperty("questions").GetArrayLength());
        Assert.Equal("[]", await client.GetStringAsync($"/api/sessions/{sessionId}/questions/instructor"));

        using var accepted = await client.PostAsync($"/api/sessions/{sessionId}/questions/generated/{id}/accept", null);
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
        var questions = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, questions.GetArrayLength());
        Assert.Equal("Light", questions[0].GetProperty("correctAnswers")[0].GetString());
        Assert.NotEmpty(questions[0].GetProperty("sourceQuote").GetString()!);
        using var duplicate = await client.PostAsync($"/api/sessions/{sessionId}/questions/generated/{id}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sessionId}/questions/instructor")).GetArrayLength());
    }

    [Theory]
    [InlineData("draft", "invalid.bin", HttpStatusCode.BadRequest)]
    [InlineData("active", "biology.txt", HttpStatusCode.Conflict)]
    [InlineData("wrong-owner", "biology.txt", HttpStatusCode.Forbidden)]
    public async Task Upload_RejectsInvalidFormatLifecycleAndOwnership(
        string scenario, string fileName, HttpStatusCode expected)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        if (scenario == "active")
            (await client.PutAsync($"/api/sessions/{sessionId}/activate", null)).EnsureSuccessStatusCode();
        if (scenario == "wrong-owner")
        {
            var sessions = factory.Services.GetRequiredService<Pulse.Common.Services.ISessionRepository>();
            var session = (await sessions.GetByIdAsync(sessionId))!;
            session.InstructorCode = "OTHER";
            sessions.Update(session);
        }
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Grounding text")), "files", fileName);
        using var response = await client.PostAsync($"/api/sessions/{sessionId}/grounding/documents", upload);
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<SurveyGenerationRepository>().GetGrounding(sessionId).Documents);
    }

    [Fact]
    public async Task Upload_InvalidFileInBatchDoesNotPartiallySave()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Valid text")), "files", "valid.txt");
        upload.Add(new ByteArrayContent([0, 1, 2]), "files", "invalid.pdf");
        var response = await client.PostAsync($"/api/sessions/{sessionId}/grounding/documents", upload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<SurveyGenerationRepository>().GetGrounding(sessionId).Documents);
    }

    [Fact]
    public async Task StudentMatchingProjection_DoesNotExposeCorrectPairing()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await CreateSession(client);
        (await client.PostAsJsonAsync($"/api/sessions/{sessionId}/questions", new
        {
            text = "Match", type = 5,
            matchingPairs = new[] { new { prompt = "HTTP", match = "Protocol" }, new { prompt = "HTML", match = "Markup" } }
        })).EnsureSuccessStatusCode();
        var questions = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sessionId}/questions");
        Assert.Equal("", questions[0].GetProperty("matchingPairs")[0].GetProperty("match").GetString());
        Assert.Equal("Markup", questions[0].GetProperty("options")[0].GetString());
        Assert.Equal("Protocol", questions[0].GetProperty("options")[1].GetString());
    }

    internal static WebApplicationFactory<ApiAssemblyMarker> CreateFactory() =>
        new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:pulse-db"] = "Filename=:memory:",
                    ["Security:InstructorCode"] = "INST"
                })));

    internal static async Task<Guid> CreateSession(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/sessions", new { title = "Grounded survey" });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }
}
