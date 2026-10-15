using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApp.Components.Pages;
using Pulse.WebApp.Services;

namespace Pulse.Tests.Tests.WebApp;

public class SurveyPageTests : BunitContext
{
    private readonly Guid _sessionId = Guid.NewGuid();

    public SurveyPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IDeviceIdService>(
            new StubDeviceIdService("7ed9232b-e5ac-4588-8690-8cc50e4c650c"));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ActiveSurvey_RendersEveryQuestionOnOnePage()
    {
        Services.AddSingleton(CreateHttpClient(CreateAllQuestionTypes()));

        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Choose one", cut.Markup);
            Assert.Contains("Choose all", cut.Markup);
            Assert.Contains("True or false", cut.Markup);
            Assert.Contains("Match the terms", cut.Markup);
            Assert.DoesNotContain("Next", cut.Markup);
            Assert.DoesNotContain("Previous", cut.Markup);
        });
    }

    [Fact]
    public void SubmitCompleteSurvey_PostsAtomicPayloadAndShowsBlindedWaitState()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        };
        var handler = new SurveyHandler(_sessionId, [question]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement($"input[value='A']").Change("A");
        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Survey submitted", cut.Markup);
            Assert.Contains("results are currently hidden", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Choose one", cut.Markup);
        });

        var submitted = Assert.IsType<SubmitSurveyRequest>(handler.SubmittedRequest);
        Assert.Equal("7ed9232b-e5ac-4588-8690-8cc50e4c650c", submitted.DeviceId);
        var answer = Assert.Single(submitted.Answers);
        Assert.Equal(question.Id, answer.QuestionId);
        Assert.Equal(["A"], answer.Values);
    }

    [Fact]
    public void RevealedResults_ReplaceBlindedWaitState()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        };
        var handler = new SurveyHandler(_sessionId, [question])
        {
            RevealResults = true
        };
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement($"input[value='A']").Change("A");
        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Results revealed", cut.Markup);
            Assert.Contains("A", cut.Markup);
            Assert.Contains(">1<", cut.Markup);
            Assert.DoesNotContain("results are currently hidden", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    private HttpClient CreateHttpClient(List<Question> questions) =>
        new(new SurveyHandler(_sessionId, questions)) { BaseAddress = new Uri("http://localhost") };

    private List<Question> CreateAllQuestionTypes() =>
    [
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        },
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "Choose all",
            Type = QuestionType.MultipleAnswer,
            Options = ["A", "B", "C"]
        },
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "True or false",
            Type = QuestionType.TrueFalse
        },
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Text = "Match the terms",
            Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = "HTTP", Match = "Protocol" },
                new MatchingPair { Prompt = "HTML", Match = "Markup" }
            ]
        }
    ];

    private sealed class StubDeviceIdService(string deviceId) : IDeviceIdService
    {
        public Task<string> GetDeviceIdAsync() => Task.FromResult(deviceId);
    }

    private sealed class SurveyHandler(Guid sessionId, List<Question> questions) : HttpMessageHandler
    {
        public SubmitSurveyRequest? SubmittedRequest { get; private set; }
        public bool RevealResults { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath == "/api/sessions/join/ABC123")
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    id = sessionId,
                    title = "Survey",
                    joinCode = "ABC123"
                });
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/questions")
            {
                return JsonResponse(HttpStatusCode.OK, questions.Select(StudentQuestionResponse.FromQuestion));
            }

            if (request.Method == HttpMethod.Post &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/submit")
            {
                SubmittedRequest = await request.Content!.ReadFromJsonAsync<SubmitSurveyRequest>(
                    cancellationToken: cancellationToken);
                return JsonResponse(HttpStatusCode.OK, new SubmitSurveyResponse(DateTime.UtcNow));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/results/student")
            {
                return RevealResults
                    ? JsonResponse(
                        HttpStatusCode.OK,
                        new StudentSurveyResultsResponse(
                            sessionId,
                            "Survey",
                            [
                                new SurveyQuestionResultsResponse(
                                    questions[0].Id,
                                    questions[0].Text,
                                    questions[0].Type.ToString(),
                                    [new OptionTallyResponse("A", 1)],
                                    1)
                            ]))
                    : JsonResponse(
                        HttpStatusCode.OK,
                        new StudentResultsStatusResponse(ResultsVisibility.Blinded));
            }

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T body) =>
            new(statusCode) { Content = JsonContent.Create(body) };
    }
}
