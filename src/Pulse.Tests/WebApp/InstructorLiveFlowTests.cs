using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Pulse.Admin.Components.Pages;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;

namespace Pulse.Tests.Tests.WebApp;

public class InstructorLiveFlowTests : BunitContext
{
    private const string InstructorCode = "INSTRUCTOR";

    [Fact]
    public async Task QuestionBank_ReopensEditsAndFiltersDrafts()
    {
        await AddInstructorStorageAsync();
        var handler = new BankDraftHandler();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuestionBank>();
        cut.WaitForElement(".bank-item");
        Assert.Contains("Draft", cut.Find(".status-chip").TextContent);
        cut.Find(".bank-item textarea").Input("Edited **question**");
        const string choice = "```csharp\nvar pair = (1, 2);\n```";
        cut.Find(".bank-option").Input(choice);
        cut.Find(".bank-taxonomy label:last-child input").Change("code, review");
        Assert.Equal("question", cut.Find(".bank-item .pulse-markdown strong").TextContent);
        Assert.Contains("var pair", cut.Find(".bank-item pre code").TextContent);
        cut.FindAll(".bank-item button").Single(button => button.TextContent == "Save as draft").Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.Saved));
        Assert.True(handler.Saved!.Value.GetProperty("isDraft").GetBoolean());
        Assert.Equal("Edited **question**", handler.Saved.Value.GetProperty("text").GetString());
        Assert.Equal(choice, handler.Saved.Value.GetProperty("options")[0].GetString());
        Assert.Equal(choice, handler.Saved.Value.GetProperty("correctAnswers")[0].GetString());
        Assert.Equal("review", handler.Saved.Value.GetProperty("tags")[1].GetString());

        cut.Find(".bank-filters select").Change("Draft");
        cut.FindAll("button").Single(button => button.TextContent == "Apply filters").Click();
        cut.WaitForAssertion(() => Assert.Contains("isDraft=true", handler.Query));
        Assert.DoesNotContain("moderationStatus=Draft", handler.Query);
    }

    [Fact]
    public async Task QuestionBank_CreatesIncompleteDraft()
    {
        await AddInstructorStorageAsync();
        var handler = new BankDraftHandler();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuestionBank>();
        cut.WaitForElement(".bank-item");
        cut.Find(".bank-create textarea").Input("Finish later");
        cut.FindAll(".bank-create button").Single(button => button.TextContent == "Save as draft").Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.Saved));
        Assert.True(handler.Saved!.Value.GetProperty("isDraft").GetBoolean());
        Assert.Equal("Finish later", handler.Saved.Value.GetProperty("text").GetString());
    }

    private sealed class BankDraftHandler : HttpMessageHandler
    {
        public JsonElement? Saved { get; private set; }
        public string Query { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(InstructorCode, request.Headers.GetValues("InstructorCode").Single());
            if (request.Method != HttpMethod.Get)
            {
                Saved = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            Query = request.RequestUri!.Query;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Items = new[]
                    {
                        new QuestionBankItem
                        {
                            Text = "Draft question", Type = QuestionType.MultipleChoice, IsDraft = true,
                            Source = QuestionSource.InstructorAuthored, Options = ["A", "B"], CorrectAnswers = ["A"]
                        }
                    },
                    TotalCount = 1
                })
            };
        }
    }

    [Fact]
    public async Task TriviaControl_OpensSelectedQuestionWithInstructorCode()
    {
        var handler = new TriviaControlHandler();
        await AddInstructorStorageAsync();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<Trivia>();
        cut.WaitForElement(".trivia-game-choice").Click();
        cut.WaitForElement(".trivia-question-row button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                $"/api/trivia/{TriviaControlHandler.GameId}/questions/{TriviaControlHandler.QuestionId}/open",
                handler.ControlPath);
            Assert.Equal(InstructorCode, handler.InstructorCode);
        });
    }

    [Fact]
    public async Task SurveyLive_RendersCountsAndInvokesReveal()
    {
        var handler = new SurveyLiveHandler();
        await AddInstructorStorageAsync();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        Services.AddSingleton(new SessionRealtimeConnectionFactory(
            "http://localhost",
            () => new HttpClientHandler()));

        var cut = Render<SurveyLive>(parameters =>
            parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("7", cut.Markup);
            Assert.Contains("3", cut.Markup);
            Assert.Contains("Live question", cut.Markup);
        });
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Reveal all results").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(handler.RevealCalled);
            Assert.Equal(InstructorCode, handler.InstructorCode);
        });
    }

    [Fact]
    public async Task SurveyLive_SharesAndRevealsQuestionsIndividually()
    {
        var handler = new SurveyLiveHandler();
        await PrepareSurveyAsync(handler);
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        cut.WaitForElement(".live-question-actions");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Share this question").Click();
        cut.WaitForAssertion(() => Assert.True(handler.ShareCalled));
        Assert.Equal(SurveyLiveHandler.QuestionId, handler.SharedQuestionId);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Reveal this question's results").Click();
        cut.WaitForAssertion(() => Assert.Contains(SurveyLiveHandler.QuestionId, handler.RevealedQuestionIds));
    }

    [Fact]
    public async Task SurveyLive_ShowsEveryOptionPercentageAndMarksCorrectAnswerInline()
    {
        await PrepareSurveyAsync(new SurveyLiveHandler());

        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        cut.WaitForElement(".correct-answer-badge");
        var rows = cut.FindAll(".result-row");
        Assert.Equal(3, rows.Count);
        Assert.Contains("3 (100%)", rows[0].TextContent);
        Assert.Contains("0 (0%)", rows[1].TextContent);
        Assert.Contains("B", rows[1].TextContent);
        Assert.Equal("Correct answer", rows[1].QuerySelector(".correct-answer-badge")!.TextContent);
        Assert.Contains("C", rows[2].TextContent);
        Assert.Empty(rows[0].QuerySelectorAll(".correct-answer-badge"));
        Assert.Empty(rows[2].QuerySelectorAll(".correct-answer-badge"));
        Assert.Empty(cut.FindAll(".survey-answer-key"));
    }

    [Fact]
    public async Task SurveyLive_WithoutResponsesShowsZeroPercentages()
    {
        await PrepareSurveyAsync(new SurveyLiveHandler { ResetCalled = true });
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        cut.WaitForElement(".correct-answer-badge");

        Assert.All(cut.FindAll(".result-row strong"), count => Assert.Equal("0 (0%)", count.TextContent));
    }

    [Theory]
    [InlineData(1, 3, "1 (33%)")]
    [InlineData(2, 3, "2 (67%)")]
    public async Task SurveyLive_PercentagesUseQuestionRespondents(int selections, int responses, string expected)
    {
        await PrepareSurveyAsync(new SurveyLiveHandler { Selections = selections, Responses = responses });
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        Assert.Equal(expected, cut.WaitForElement(".result-row strong").TextContent);
    }

    [Fact]
    public async Task SurveyLive_ResetRequiresConfirmation()
    {
        var handler = new SurveyLiveHandler();
        await PrepareSurveyAsync(handler);
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));
        cut.WaitForElement(".live-actions").QuerySelectorAll("button").Single(button => button.TextContent == "Reset survey").Click();

        Assert.False(handler.ResetCalled);
    }

    [Fact]
    public async Task SurveyLive_CancelResetPreservesResponses()
    {
        var handler = new SurveyLiveHandler();
        await PrepareSurveyAsync(handler);
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));
        cut.WaitForElement(".live-actions").QuerySelectorAll("button").Single(button => button.TextContent == "Reset survey").Click();

        cut.WaitForElement("[role='alertdialog']").QuerySelectorAll("button").Single(button => button.TextContent == "Cancel").Click();

        Assert.False(handler.ResetCalled);
    }

    [Fact]
    public async Task SurveyLive_ConfirmedResetUsesInstructorCode()
    {
        var handler = new SurveyLiveHandler();
        await PrepareSurveyAsync(handler);
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));
        cut.WaitForElement(".live-actions").QuerySelectorAll("button").Single(button => button.TextContent == "Reset survey").Click();

        cut.WaitForElement("[data-testid='confirm-reset']").Click();

        cut.WaitForAssertion(() => Assert.Equal(InstructorCode, handler.InstructorCode));
    }

    [Fact]
    public async Task SurveyLive_ConfirmedResetRefreshesCompletionCount()
    {
        var handler = new SurveyLiveHandler();
        await PrepareSurveyAsync(handler);
        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));
        cut.WaitForElement(".live-actions").QuerySelectorAll("button").Single(button => button.TextContent == "Reset survey").Click();

        cut.WaitForElement("[data-testid='confirm-reset']").Click();

        cut.WaitForAssertion(() => Assert.All(cut.FindAll(".live-stat strong"), count => Assert.Equal("0", count.TextContent)));
    }

    [Fact]
    public async Task SurveyLive_LinksToEditor()
    {
        await PrepareSurveyAsync(new SurveyLiveHandler());

        var cut = Render<SurveyLive>(parameters => parameters.Add(page => page.SessionId, SurveyLiveHandler.SessionId));

        Assert.Equal($"/instructor/questions/{SurveyLiveHandler.SessionId}", cut.WaitForElement(".live-actions a").GetAttribute("href"));
    }

    [Theory]
    [InlineData(SessionStatus.Active)]
    [InlineData(SessionStatus.Closed)]
    public async Task QuestionManagement_OffersEditingOutsideDraft(SessionStatus status)
    {
        var handler = new EditorHandler { Status = status };
        await PrepareEditorAsync(handler);

        var cut = Render<QuestionManagement>(parameters => parameters.Add(page => page.SessionId, handler.SessionId.ToString()));

        Assert.Equal("Edit survey", cut.WaitForElement("[data-testid='begin-edit-survey']").TextContent);
    }

    [Fact]
    public async Task QuestionManagement_CancelDoesNotConfirmResponseClearing()
    {
        var handler = new EditorHandler { HasResponses = true };
        await PrepareEditorAsync(handler);
        var cut = Render<QuestionManagement>(parameters => parameters.Add(page => page.SessionId, handler.SessionId.ToString()));
        cut.WaitForElement("[data-testid='begin-edit-survey']").Click();
        cut.WaitForElement("[data-testid='cancel-edit-survey']").Click();

        Assert.False(handler.ClearingConfirmed);
    }

    [Fact]
    public async Task QuestionManagement_ConfirmSendsExplicitConsent()
    {
        var handler = new EditorHandler { HasResponses = true };
        await PrepareEditorAsync(handler);
        var cut = Render<QuestionManagement>(parameters => parameters.Add(page => page.SessionId, handler.SessionId.ToString()));
        cut.WaitForElement("[data-testid='begin-edit-survey']").Click();

        cut.WaitForElement("[data-testid='confirm-edit-survey']").Click();

        cut.WaitForAssertion(() => Assert.True(handler.ClearingConfirmed));
    }

    [Fact]
    public async Task QuestionManagement_NoResponsesPausesWithoutConfirmation()
    {
        var handler = new EditorHandler();
        await PrepareEditorAsync(handler);
        var cut = Render<QuestionManagement>(parameters => parameters.Add(page => page.SessionId, handler.SessionId.ToString()));

        cut.WaitForElement("[data-testid='begin-edit-survey']").Click();

        cut.WaitForAssertion(() => Assert.Contains("This survey is paused for editing", cut.Markup));
    }

    [Fact]
    public async Task QuestionManagement_EditRequestsUseInstructorCode()
    {
        var handler = new EditorHandler();
        await PrepareEditorAsync(handler);
        var cut = Render<QuestionManagement>(parameters => parameters.Add(page => page.SessionId, handler.SessionId.ToString()));

        cut.WaitForElement("[data-testid='begin-edit-survey']").Click();

        cut.WaitForAssertion(() => Assert.Equal(InstructorCode, handler.InstructorCode));
    }

    private async Task PrepareEditorAsync(EditorHandler handler)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        await AddInstructorStorageAsync();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        Services.AddHttpClient("survey-generation", client => client.BaseAddress = new Uri("http://localhost"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        Render<MudBlazor.MudPopoverProvider>();
    }

    private async Task PrepareSurveyAsync(SurveyLiveHandler handler)
    {
        await AddInstructorStorageAsync();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        Services.AddSingleton(new SessionRealtimeConnectionFactory("http://localhost", () => new HttpClientHandler()));
    }

    private async Task AddInstructorStorageAsync()
    {
        var javaScript = new MemoryStorageJsRuntime();
        var storage = new ProtectedSessionStorage(javaScript, new EphemeralDataProtectionProvider());
        await storage.SetAsync("instructorCode", InstructorCode);
        var stored = await storage.GetAsync<string>("instructorCode");
        Assert.True(stored.Success, string.Join(", ", javaScript.Calls));
        Assert.Equal(InstructorCode, stored.Value);
        Services.AddSingleton(storage);
    }

    private sealed class MemoryStorageJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string?> values = [];
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Calls.Add($"{identifier} ({args?.Length ?? 0})");
            if (identifier == "sessionStorage.setItem")
            {
                values[args![0]!.ToString()!] = args[1]?.ToString();
                return ValueTask.FromResult(default(TValue)!);
            }

            if (identifier == "sessionStorage.getItem")
            {
                if (values.TryGetValue(args![0]!.ToString()!, out var value))
                    return ValueTask.FromResult((TValue)(object)value!);
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class TriviaControlHandler : HttpMessageHandler
    {
        public static readonly Guid GameId = Guid.NewGuid();
        public static readonly Guid QuestionId = Guid.NewGuid();
        public string? ControlPath { get; private set; }
        public string? InstructorCode { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && path == "/api/trivia")
                return Task.FromResult(Json(new[] { Game() }));
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/questionbank", StringComparison.Ordinal))
                return Task.FromResult(Json(new { items = Array.Empty<QuestionBankItem>(), totalCount = 0 }));
            if (request.Method == HttpMethod.Get && path == $"/api/trivia/{GameId}/control")
                return Task.FromResult(Json(Control()));
            if (request.Method == HttpMethod.Put)
            {
                ControlPath = request.RequestUri.AbsolutePath;
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(Json(Game()));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static TriviaGame Game() => new()
        {
            Id = GameId,
            Title = "Instructor game",
            InstructorCode = InstructorLiveFlowTests.InstructorCode,
            JoinCode = "ABC123"
        };

        private static InstructorTriviaStateResponse Control() => new(
            new TriviaGameStateResponse(
                GameId,
                "Instructor game",
                "ABC123",
                TriviaCompetitionMode.Individual,
                TriviaGameStatus.Lobby,
                null,
                []),
            [new TriviaQuestion { Id = QuestionId, GameId = GameId, Text = "Open me" }],
            [],
            2);
    }

    private sealed class SurveyLiveHandler : HttpMessageHandler
    {
        public static readonly Guid SessionId = Guid.NewGuid();
        public static readonly Guid QuestionId = Guid.NewGuid();
        public bool RevealCalled { get; private set; }
        public bool ShareCalled { get; private set; }
        public Guid? SharedQuestionId { get; private set; }
        public List<Guid> RevealedQuestionIds { get; } = [];
        public bool ResetCalled { get; set; }
        public int Selections { get; init; } = 3;
        public int Responses { get; init; } = 3;
        public string? InstructorCode { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/results")
            {
                return Task.FromResult(Json(new InstructorSurveyResultsResponse(
                    SessionId,
                    "Live survey",
                    ResetCalled ? 0 : 7,
                    ResetCalled ? 0 : 3,
                    [new SurveyQuestionResultsResponse(
                        QuestionId,
                        "Live question",
                        "MultipleChoice",
                        [new OptionTallyResponse("A", ResetCalled ? 0 : Selections),
                         new OptionTallyResponse("B", 0), new OptionTallyResponse("C", 0)],
                        ResetCalled ? 0 : Responses,
                        ["B"])],
                    SharedQuestionId,
                    RevealedQuestionIds)));
            }

            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/unblind")
            {
                RevealCalled = true;
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(Json(new { resultsVisibility = ResultsVisibility.Revealed }));
            }

            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/questions/{QuestionId}/share")
            {
                ShareCalled = true;
                SharedQuestionId = QuestionId;
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/questions/{QuestionId}/reveal")
            {
                RevealedQuestionIds.Add(QuestionId);
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/reset")
            {
                ResetCalled = true;
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class EditorHandler : HttpMessageHandler
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        public bool HasResponses { get; init; }
        public bool ClearingConfirmed { get; private set; }
        public string? InstructorCode { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put && path == $"/api/sessions/{SessionId}/edit")
            {
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                ClearingConfirmed = request.RequestUri.Query == "?clearResponses=true";
                if (HasResponses && !ClearingConfirmed)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));

                Status = SessionStatus.Draft;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            if (path == $"/api/sessions/{SessionId}")
                return Task.FromResult(Json(new Session { Id = SessionId, Title = "Survey", Status = Status }));
            if (path == $"/api/sessions/{SessionId}/questions/instructor")
                return Task.FromResult(Json(Array.Empty<Question>()));
            if (path == $"/api/sessions/{SessionId}/grounding")
                return Task.FromResult(Json(new SurveyGroundingResponse(Guid.NewGuid(), string.Empty, [])));
            if (path == "/api/questionbank")
                return Task.FromResult(Json(new { items = Array.Empty<QuestionBankItem>(), totalCount = 0 }));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static HttpResponseMessage Json<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
