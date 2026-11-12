using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Admin.Components;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests.WebApp;

public class SurveyQuestionGeneratorTests : BunitContext
{
    private readonly GeneratorHandler handler = new();

    public SurveyQuestionGeneratorTests() =>
        Services.AddSingleton<IHttpClientFactory>(new ClientFactory(handler));

    [Fact]
    public void Generation_RequiresPreviewReviewBeforeSaving()
    {
        var saved = 0;
        var cut = Render<SurveyQuestionGenerator>(parameters => parameters
            .Add(component => component.SessionId, handler.SessionId)
            .Add(component => component.InstructorCode, "INST")
            .Add(component => component.IsDraft, true)
            .Add(component => component.QuestionsSaved, () => saved++));

        cut.WaitForElement("#grounding-context").Change("Photosynthesis uses light.");
        cut.Find("#generation-count").Change("2");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Generate question preview")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Review before adding", cut.Markup);
            Assert.Contains("(Correct answer)", cut.Markup);
            Assert.Contains("Photosynthesis uses light.", cut.Find("blockquote").TextContent);
            Assert.Contains("Choice A", cut.Markup);
        });
        Assert.Equal(2, handler.RequestedCount);
        Assert.False(handler.Accepted);
        Assert.Equal(0, saved);
        cut.FindAll("button").Single(button => button.TextContent.Contains("Approve and add")).Click();
        cut.WaitForAssertion(() => Assert.Equal(1, saved));
        Assert.True(handler.Accepted);
        Assert.Equal("INST", handler.InstructorCode);
    }

    [Fact]
    public void Preview_EditsMarkdownChoicesAndTagsBeforeApproval()
    {
        var cut = RenderPreview();
        cut.Find(".preview-text").Input("What is **energy**?");
        cut.Find(".preview-option").Input("```csharp\nvar light = true;\n```");
        cut.Find(".preview-tags").Change("biology, code, BIOLOGY");
        Assert.Equal("energy", cut.Find("strong").TextContent);
        Assert.Contains("var light = true;", cut.Find("pre code").TextContent);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Approve and add")).Click();
        cut.WaitForAssertion(() => Assert.True(handler.Accepted));
        var edited = Assert.Single(handler.UpdatedQuestions!);
        Assert.Equal("What is **energy**?", edited.Text);
        Assert.Equal("```csharp\nvar light = true;\n```", edited.CorrectAnswer);
        Assert.Equal(["biology", "code"], edited.Tags);
    }

    [Fact]
    public void Preview_AddsAndRemovesChoicesWithLetterLabels()
    {
        var cut = RenderPreview();
        var originalCount = cut.FindAll(".preview-option").Count;
        Assert.Contains("Choice A", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Add choice").Click();
        Assert.Equal(originalCount + 1, cut.FindAll(".preview-option").Count);

        cut.FindAll("button").Last(button => button.TextContent.Trim() == "Remove choice").Click();
        Assert.Equal(originalCount, cut.FindAll(".preview-option").Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Preview_SaveIndividualQuestionToBank(bool isDraft)
    {
        var cut = RenderPreview();
        cut.Find(".preview-text").Input("Review **later**");
        cut.Find(".preview-tags").Change("biology, review");
        cut.FindAll("button").Single(button => button.TextContent.Trim() ==
            (isDraft ? "Save as draft" : "Save question to bank")).Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.BankItem));
        Assert.Equal(isDraft, handler.BankItem!.IsDraft);
        Assert.Equal("Review **later**", handler.BankItem.Text);
        Assert.Equal(["biology", "review"], handler.BankItem.Tags);
        Assert.Equal("Photosynthesis uses light.", handler.BankItem.SourceQuote);
        Assert.Equal(["Light"], handler.BankItem.CorrectAnswers);
        Assert.Empty(cut.FindAll(".question-card"));
        Assert.False(handler.Accepted);
    }

    [Fact]
    public void Preview_RemoveDoesNotSaveAndDisablesEmptyApproval()
    {
        var cut = RenderPreview();
        cut.FindAll("button").Single(button => button.TextContent == "Remove question").Click();
        Assert.Empty(cut.FindAll(".question-card"));
        Assert.True(cut.FindAll("button").Single(button => button.TextContent.Contains("Approve and add")).HasAttribute("disabled"));
        Assert.Null(handler.BankItem);
        Assert.False(handler.Accepted);
    }

    [Fact]
    public void Preview_FailedBankSaveKeepsEditsAndDisplaysError()
    {
        handler.FailBankSave = true;
        var cut = RenderPreview();
        cut.FindAll("button").Single(button => button.TextContent == "Save as draft").Click();
        cut.WaitForAssertion(() => Assert.Contains("Could not save draft", cut.Find("[role='alert']").TextContent));
        Assert.Single(cut.FindAll(".question-card"));
    }

    private IRenderedComponent<SurveyQuestionGenerator> RenderPreview()
    {
        var cut = Render<SurveyQuestionGenerator>(parameters => parameters
            .Add(component => component.SessionId, handler.SessionId)
            .Add(component => component.InstructorCode, "INST")
            .Add(component => component.IsDraft, true));
        cut.WaitForElement("#grounding-context");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Generate question preview")).Click();
        cut.WaitForElement(".question-card");
        return cut;
    }

    [Fact]
    public void GenerationError_IsShownInsteadOfPreview()
    {
        handler.FailGeneration = true;
        var cut = Render<SurveyQuestionGenerator>(parameters => parameters
            .Add(component => component.SessionId, handler.SessionId)
            .Add(component => component.InstructorCode, "INST")
            .Add(component => component.IsDraft, true));
        cut.WaitForElement("#grounding-context").Change("Photosynthesis uses light.");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Generate question preview")).Click();
        cut.WaitForAssertion(() => Assert.Contains("Start Ollama", cut.Find("[role='alert']").TextContent));
        Assert.Empty(cut.FindAll("[aria-label='Generated question preview']"));
        Assert.False(handler.Accepted);
    }

    [Fact]
    public void ActiveSurvey_DisablesGroundingAndGeneration()
    {
        var cut = Render<SurveyQuestionGenerator>(parameters => parameters
            .Add(component => component.SessionId, handler.SessionId)
            .Add(component => component.InstructorCode, "INST")
            .Add(component => component.IsDraft, false));
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.Contains("only be changed while the survey is in Draft", cut.Markup);
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("survey-generation", name);
            return new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost") };
        }
    }

    private sealed class GeneratorHandler : HttpMessageHandler
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        private readonly Guid previewId = Guid.NewGuid();
        public bool Accepted { get; private set; }
        public bool FailGeneration { get; set; }
        public int RequestedCount { get; private set; }
        public string? InstructorCode { get; private set; }
        public List<GeneratedSurveyQuestion>? UpdatedQuestions { get; private set; }
        public QuestionBankItem? BankItem { get; private set; }
        public bool FailBankSave { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InstructorCode = request.Headers.GetValues("InstructorCode").Single();
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/questionbank")
            {
                if (FailBankSave)
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create("Could not save draft.") };
                BankItem = await request.Content!.ReadFromJsonAsync<QuestionBankItem>(cancellationToken);
                return Json(BankItem);
            }
            if (request.Method == HttpMethod.Put && path.EndsWith($"/questions/generated/{previewId}", StringComparison.Ordinal))
            {
                UpdatedQuestions = (await request.Content!.ReadFromJsonAsync<UpdateGeneratedSurveyPreviewRequest>(cancellationToken))!.Questions;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.EndsWith("/grounding", StringComparison.Ordinal) || path.EndsWith("/grounding/context", StringComparison.Ordinal))
                return Json(new SurveyGroundingResponse(Guid.NewGuid(), "Photosynthesis uses light.", []));
            if (path.EndsWith("/questions/generate", StringComparison.Ordinal))
            {
                RequestedCount = (await request.Content!.ReadFromJsonAsync<GenerateSurveyQuestionsRequest>(cancellationToken))!.Count;
                if (FailGeneration)
                    return new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = JsonContent.Create(new { detail = "Start Ollama before generating." }) };
                return Json(new GeneratedSurveyPreviewResponse(previewId,
                [
                    new GeneratedSurveyQuestion
                    {
                        Text = "What energy is used?", Options = ["Light", "Heat", "Sound", "Gravity"],
                        CorrectAnswer = "Light", Explanation = "The material describes light.",
                        SourceId = "context:1", SourceQuote = "Photosynthesis uses light."
                    }
                ]));
            }
            if (path.EndsWith($"/questions/generated/{previewId}/accept", StringComparison.Ordinal))
            {
                Accepted = true;
                return Json(Array.Empty<object>());
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
