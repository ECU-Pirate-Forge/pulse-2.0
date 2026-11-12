using System.Net.Http.Json;
using LiteDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests;

public class QuestionDraftTests
{
    private static WebApplicationFactory<ApiAssemblyMarker> CreateFactory() =>
        SurveyGenerationApiTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<LiteDatabase>();
            services.AddSingleton(_ => new LiteDatabase(new MemoryStream()));
        }));

    [Fact]
    public async Task Draft_PersistsCanBeEditedAndCannotBeUsedUntilApproved()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var sessionId = await SurveyGenerationApiTests.CreateSession(client);
        using var created = await client.PostAsJsonAsync("/api/questionbank", new
        {
            text = "Draft **question**", type = 0, isDraft = true,
            options = new[] { "Unfinished" }, tags = new[] { " code ", "CODE", "review" },
            correctAnswers = new[] { "Unfinished" }, explanation = "Explanation",
            sourceId = "context:1", sourceQuote = "Original source"
        });
        created.EnsureSuccessStatusCode();
        var item = (await created.Content.ReadFromJsonAsync<QuestionBankItem>())!;
        Assert.True(item.IsDraft);
        Assert.Equal(["code", "review"], item.Tags);

        using var reopened = factory.CreateClient();
        reopened.DefaultRequestHeaders.Add("InstructorCode", "INST");
        var drafts = await reopened.GetFromJsonAsync<QuestionBankListResponse>("/api/questionbank?isDraft=true&tag=CODE");
        Assert.Equal(item.Id, Assert.Single(drafts!.Items).Id);
        var available = await reopened.GetFromJsonAsync<QuestionBankListResponse>("/api/questionbank?moderationStatus=Approved");
        Assert.Empty(available!.Items);
        using var blocked = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/questions/import",
            new { questionBankItemIds = new[] { item.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        using var incomplete = await client.PutAsJsonAsync($"/api/questionbank/{item.Id}",
            new { moderationStatus = 0, isDraft = false });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.True(factory.Services.GetRequiredService<IQuestionBankRepository>().GetById(item.Id)!.IsDraft);

        const string choice = "```csharp\nConsole.WriteLine(\"hello, world\");\n```";
        using var updated = await reopened.PutAsJsonAsync($"/api/questionbank/{item.Id}", new
        {
            moderationStatus = 0, isDraft = false, text = "Reviewed **question**",
            options = new[] { choice, "Other" }, correctAnswers = new[] { choice },
            tags = new[] { "code", "approved" }
        });
        updated.EnsureSuccessStatusCode();
        var published = (await updated.Content.ReadFromJsonAsync<QuestionBankItem>())!;
        Assert.False(published.IsDraft);
        Assert.Equal("Original source", published.SourceQuote);
        Assert.Equal("Explanation", published.Explanation);
        using var imported = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/questions/import",
            new { questionBankItemIds = new[] { item.Id } });
        imported.EnsureSuccessStatusCode();
        var saved = Assert.Single((await imported.Content.ReadFromJsonAsync<List<Question>>())!);
        Assert.Equal(choice, saved.Options[0]);
        Assert.Equal([choice], saved.CorrectAnswers);
        Assert.Equal(["code", "approved"], saved.Tags);
    }

    [Fact]
    public async Task Draft_ExportImportPreservesDraftAndRejectsSurveyUse()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        using var created = await client.PostAsJsonAsync("/api/questionbank", new
        {
            text = "Unfinished question", type = 0, isDraft = true
        });
        created.EnsureSuccessStatusCode();
        var item = (await created.Content.ReadFromJsonAsync<QuestionBankItem>())!;
        var document = (await client.GetFromJsonAsync<ContentTransferDocument>($"/api/content/questions?questionId={item.Id}"))!;
        Assert.True(Assert.Single(document.Questions!).IsDraft);
        using var imported = await client.PostAsJsonAsync("/api/content/import/questions", document);
        imported.EnsureSuccessStatusCode();
        var drafts = await client.GetFromJsonAsync<QuestionBankListResponse>("/api/questionbank?isDraft=true");
        Assert.Equal(2, drafts!.TotalCount);
        var sessionId = await SurveyGenerationApiTests.CreateSession(client);
        using var blocked = await client.PostAsJsonAsync($"/api/content/import/questions?sessionId={sessionId}", document);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }

    [Fact]
    public async Task Draft_IsExcludedFromTrivia()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("InstructorCode", "INST");
        using var created = await client.PostAsJsonAsync("/api/questionbank", new
        {
            text = "Not ready", type = 0, isDraft = true, triviaEligible = true
        });
        created.EnsureSuccessStatusCode();
        var available = await client.GetFromJsonAsync<QuestionBankListResponse>(
            "/api/questionbank?moderationStatus=Approved&triviaEligible=true");
        Assert.Empty(available!.Items);
    }
}
