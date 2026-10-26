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
    public void MarkdownQuestionsAndChoices_RenderWithoutChangingSubmittedValues()
    {
        const string choice = "```csharp\nConsole.WriteLine(\"hello\");\n```";
        var question = new Question
        {
            SessionId = _sessionId, Text = "Read **carefully** <script>alert(1)</script>",
            Type = QuestionType.MultipleChoice, Options = [choice, "Other"]
        };
        var handler = new SurveyHandler(_sessionId, [question]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("pre code");
        Assert.Equal("carefully", cut.Find("strong").TextContent);
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("Console.WriteLine", cut.Find("pre code").TextContent);
        cut.Find("input[type=radio]").Change(choice);
        cut.Find("[data-testid='submit-survey']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.SubmittedRequest));
        Assert.Equal([choice], Assert.Single(handler.SubmittedRequest!.Answers).Values);
    }

    [Fact]
    public void MatchingChoices_RenderMarkdownAndSubmitOriginalValuesInPromptOrder()
    {
        const string code = "```csharp\nreturn 1;\n```";
        var question = new Question
        {
            SessionId = _sessionId, Text = "Match **code**", Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = "**First**", Match = code },
                new MatchingPair { Prompt = "Second", Match = "Other" }
            ]
        };
        var handler = new SurveyHandler(_sessionId, [question]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement(".matching-row pre code");
        cut.FindAll(".matching-row")[0].QuerySelectorAll("input").Single(input => input.GetAttribute("value") == code).Change(code);
        cut.FindAll(".matching-row")[1].QuerySelectorAll("input").Single(input => input.GetAttribute("value") == "Other").Change("Other");
        cut.Find("[data-testid='submit-survey']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.SubmittedRequest));
        Assert.Equal([code, "Other"], Assert.Single(handler.SubmittedRequest!.Answers).Values);
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
            Assert.Contains("1 (100%)", cut.Markup);
            Assert.DoesNotContain("results are currently hidden", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ActiveSurvey_DoesNotDisplayAnswerKey()
    {
        var handler = new SurveyHandler(_sessionId, [SingleQuestion()]) { RevealResults = true };
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("[data-testid='submit-survey']");

        Assert.Empty(cut.FindAll(".survey-answer-key"));
        Assert.Empty(cut.FindAll(".correct-answer-badge"));
    }

    [Fact]
    public void BlindedSubmission_DoesNotDisplayAnswerKey()
    {
        var handler = new SurveyHandler(_sessionId, [SingleQuestion()]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");

        cut.Find("[data-testid='submit-survey']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Survey submitted", cut.Markup));

        Assert.Empty(cut.FindAll(".survey-answer-key"));
        Assert.Empty(cut.FindAll(".correct-answer-badge"));
    }

    [Fact]
    public void RevealedResults_DisplayUnselectedCorrectAnswer()
    {
        var handler = new SurveyHandler(_sessionId, [SingleQuestion()]) { RevealResults = true };
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");

        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForElement(".correct-answer-badge");
        var rows = cut.FindAll(".survey-result-row");
        Assert.Equal(2, rows.Count);
        Assert.Contains("1 (100%)", rows[0].TextContent);
        Assert.Empty(rows[0].QuerySelectorAll(".correct-answer-badge"));
        Assert.Contains("B", rows[1].TextContent);
        Assert.Contains("0 (0%)", rows[1].TextContent);
        Assert.Equal("Correct answer", rows[1].QuerySelector(".correct-answer-badge")!.TextContent);
        Assert.Empty(cut.FindAll(".survey-answer-key"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevealedResults_MarksOnlyConfiguredCorrectOptions(bool hasAnswerKey)
    {
        var question = SingleQuestion();
        question.CorrectAnswers = hasAnswerKey ? ["A", "B"] : [];
        var handler = new SurveyHandler(_sessionId, [question]) { RevealResults = true };
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");

        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForElement(".survey-result-row");
        Assert.Equal(hasAnswerKey ? 2 : 0, cut.FindAll(".correct-answer-badge").Count);
        Assert.Empty(cut.FindAll(".survey-answer-key"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetSurvey_AllowsParticipantToSubmitAgain(bool initiallyRevealed)
    {
        var handler = new SurveyHandler(_sessionId, [SingleQuestion()]) { RevealResults = initiallyRevealed };
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");
        cut.Find("[data-testid='submit-survey']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='submit-survey']")));

        handler.ResetVersion = 1;
        handler.RevealResults = false;
        cut.WaitForElement("[data-testid='submit-survey']", TimeSpan.FromSeconds(8));
        cut.Find("input[value='B']").Change("B");
        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, handler.SubmissionCount));
    }

    [Fact]
    public void EditingSurvey_HidesSubmissionForm()
    {
        var handler = new SurveyHandler(_sessionId, [SingleQuestion()]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");

        handler.Status = SessionStatus.Draft;
        handler.ResetVersion = 1;
        cut.WaitForElement("[data-testid='survey-paused']", TimeSpan.FromSeconds(8));

        Assert.Empty(cut.FindAll("[data-testid='submit-survey']"));
    }

    [Fact]
    public void EditedSurvey_ActivationReloadsQuestionsBeforeSubmission()
    {
        var question = SingleQuestion();
        var handler = new SurveyHandler(_sessionId, [question]);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var cut = Render<QuizPage>(parameters => parameters.Add(page => page.JoinCode, "ABC123"));
        cut.WaitForElement("input[value='A']").Change("A");
        handler.Status = SessionStatus.Draft;
        handler.ResetVersion = 1;
        cut.WaitForElement("[data-testid='survey-paused']", TimeSpan.FromSeconds(8));

        question.Text = "Updated question";
        question.Options = ["C", "D"];
        question.CorrectAnswers = ["D"];
        handler.Status = SessionStatus.Active;
        cut.WaitForElement("input[value='D']", TimeSpan.FromSeconds(8)).Change("D");
        cut.Find("[data-testid='submit-survey']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["D"], handler.SubmittedRequest?.Answers.Single().Values));
    }

    private Question SingleQuestion() => new()
    {
        SessionId = _sessionId, Text = "Choose one", Type = QuestionType.MultipleChoice,
        Options = ["A", "B"], CorrectAnswers = ["B"]
    };

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
        public bool RevealResults { get; set; }
        public int ResetVersion { get; set; }
        public int SubmissionCount { get; private set; }
        public SessionStatus Status { get; set; } = SessionStatus.Active;

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
                    joinCode = "ABC123",
                    resetVersion = ResetVersion
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
                SubmissionCount++;
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
                                    questions[0].Options.Select(option => new OptionTallyResponse(option, option == "A" ? 1 : 0)).ToList(),
                                    1,
                                    questions[0].CorrectAnswers)
                            ], ResetVersion))
                    : JsonResponse(
                        HttpStatusCode.OK,
                        new StudentResultsStatusResponse(ResultsVisibility.Blinded, ResetVersion, Status));
            }

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T body) =>
            new(statusCode) { Content = JsonContent.Create(body) };
    }
}
