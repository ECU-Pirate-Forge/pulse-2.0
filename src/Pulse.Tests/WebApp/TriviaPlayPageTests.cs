using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApp.Components.Pages;
using Pulse.WebApp.Services;

namespace Pulse.Tests.Tests.WebApp;

public class TriviaPlayPageTests : BunitContext
{
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();

    public TriviaPlayPageTests()
    {
        Services.AddSingleton<IDeviceIdService>(
            new StubDeviceIdService("7ed9232b-e5ac-4588-8690-8cc50e4c650c"));
    }

    [Fact]
    public void SharedJoinRoute_OpensGameWithoutReenteringCode()
    {
        Assert.Contains(typeof(TriviaPlay).GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Components.RouteAttribute), false),
            attribute => ((Microsoft.AspNetCore.Components.RouteAttribute)attribute).Template == "/trivia/{JoinCode}");

        var handler = new TriviaHandler();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<TriviaPlay>(parameters => parameters.Add(p => p.JoinCode, " abc123 "));

        cut.WaitForElement("input[maxlength='30']").Change("Ada");
        Assert.Equal("ABC123", cut.Find("input[maxlength='6']").GetAttribute("value"));
        cut.FindAll("button").Single().Click();
        cut.WaitForAssertion(() => Assert.Contains("Which option?", cut.Markup));
    }

    [Fact]
    public void IndividualGame_JoinAndSubmitAnswer_CompletesCriticalStudentPath()
    {
        var handler = new TriviaHandler();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<TriviaPlay>();

        cut.Find("input[maxlength='6']").Change("abc123");
        cut.Find("button").Click();
        cut.WaitForElement("input[maxlength='30']").Change("Ada");
        cut.FindAll("button").Single().Click();

        cut.WaitForAssertion(() => Assert.Contains("Which option?", cut.Markup));
        cut.Find("input[type='checkbox']").Change(true);
        cut.FindAll("button").Single().Click();

        cut.WaitForAssertion(() => Assert.Contains("Answer submitted · 125 points", cut.Markup));
        Assert.Equal(["A"], handler.SubmittedAnswer?.Values);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, false, "That trivia code was not found.")]
    [InlineData(HttpStatusCode.InternalServerError, false, "could not check that trivia game")]
    [InlineData(HttpStatusCode.OK, true, "could not connect")]
    public void SharedJoinRoute_LookupFailureShowsActionableError(
        HttpStatusCode status, bool connectionFailure, string expectedMessage)
    {
        Services.AddSingleton(new HttpClient(new FailedLookupHandler(status, connectionFailure))
        {
            BaseAddress = new Uri("http://localhost")
        });

        var cut = Render<TriviaPlay>(parameters => parameters.Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.Contains(expectedMessage, cut.Find("[role='alert']").TextContent));
        Assert.Empty(cut.FindAll("input[maxlength='30']"));
        Assert.Contains("Find game", cut.Markup);
    }

    private sealed class FailedLookupHandler(HttpStatusCode status, bool connectionFailure)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            connectionFailure
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection failed"))
                : Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class StubDeviceIdService(string deviceId) : IDeviceIdService
    {
        public Task<string> GetDeviceIdAsync() => Task.FromResult(deviceId);
    }

    private sealed class TriviaHandler : HttpMessageHandler
    {
        public SubmitTriviaAnswerRequest? SubmittedAnswer { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/trivia/join/ABC123")
            {
                return Json(new
                {
                    id = GameId,
                    title = "Mobile trivia",
                    competitionMode = TriviaCompetitionMode.Individual,
                    teams = Array.Empty<TriviaTeam>()
                });
            }

            if (request.Method == HttpMethod.Post && path == "/api/trivia/join/ABC123")
            {
                return Json(new JoinTriviaResponse(
                    GameId, ParticipantId, "Mobile trivia", TriviaCompetitionMode.Individual));
            }

            if (request.Method == HttpMethod.Get &&
                path == $"/api/trivia/{GameId}/state/{ParticipantId}")
            {
                return Json(new TriviaGameStateResponse(
                    GameId,
                    "Mobile trivia",
                    "ABC123",
                    TriviaCompetitionMode.Individual,
                    TriviaGameStatus.QuestionOpen,
                    new TriviaQuestionView(
                        QuestionId,
                        "Which option?",
                        "MultipleChoice",
                        ["A", "B"],
                        DateTime.UtcNow,
                        20),
                    []));
            }

            if (request.Method == HttpMethod.Post &&
                path == $"/api/trivia/{GameId}/questions/{QuestionId}/answers")
            {
                SubmittedAnswer = await request.Content!.ReadFromJsonAsync<SubmitTriviaAnswerRequest>(
                    cancellationToken: cancellationToken);
                return Json(new SubmitTriviaAnswerResponse(true, 125));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T body) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
