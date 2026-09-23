using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
        cut.FindAll("button").Single(button => button.TextContent.Contains("Reveal")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(handler.RevealCalled);
            Assert.Equal(InstructorCode, handler.InstructorCode);
        });
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
        public bool RevealCalled { get; private set; }
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
                    7,
                    3,
                    [new SurveyQuestionResultsResponse(
                        Guid.NewGuid(),
                        "Live question",
                        "MultipleChoice",
                        [new OptionTallyResponse("A", 3)],
                        3)])));
            }

            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath == $"/api/sessions/{SessionId}/unblind")
            {
                RevealCalled = true;
                InstructorCode = request.Headers.GetValues("InstructorCode").Single();
                return Task.FromResult(Json(new { resultsVisibility = ResultsVisibility.Revealed }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static HttpResponseMessage Json<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
