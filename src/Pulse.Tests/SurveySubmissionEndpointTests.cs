using System.Text.Json;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class SurveySubmissionEndpointTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly string _deviceId = Guid.NewGuid().ToString();

    [Fact]
    public async Task SubmitSurvey_ValidCompletePayload_StoresOneStructuredResponsePerQuestion()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questionRepo = CreateQuestions(database);
        var responseRepo = new ResponseRepository(database);

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            CreateValidRequest(),
            CreateSessionRepository(SessionStatus.Active).Object,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<SubmitSurveyResponse>>(result);
        Assert.NotEqual(default, ok.Value!.SubmittedAt);

        var responses = responseRepo.GetBySessionAndDevice(_sessionId, _deviceId).ToList();
        Assert.Equal(4, responses.Count);
        Assert.Equal(["A", "C"], System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            responses.Single(response => response.QuestionId == QuestionId(1)).Value));
        Assert.Equal(["Protocol", "Markup"], System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            responses.Single(response => response.QuestionId == QuestionId(3)).Value));
        Assert.Single(responseRepo.GetSubmissionsBySession(_sessionId));
    }

    [Fact]
    public async Task SubmitSurvey_RepeatedPayload_ReplacesAnswersWithoutAddingRowsOrCompletions()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questionRepo = CreateQuestions(database);
        var responseRepo = new ResponseRepository(database);
        var sessionRepo = CreateSessionRepository(SessionStatus.Active).Object;

        await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            CreateValidRequest(),
            sessionRepo,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        var changed = CreateValidRequest() with
        {
            Answers =
            [
                new SurveyAnswerRequest(QuestionId(0), ["B"]),
                new SurveyAnswerRequest(QuestionId(1), ["B", "C"]),
                new SurveyAnswerRequest(QuestionId(2), ["False"]),
                new SurveyAnswerRequest(QuestionId(3), ["Markup", "Protocol"])
            ]
        };
        await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            changed,
            sessionRepo,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        var responses = responseRepo.GetBySessionAndDevice(_sessionId, _deviceId).ToList();
        Assert.Equal(4, responses.Count);
        Assert.Equal(["B"], System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            responses.Single(response => response.QuestionId == QuestionId(0)).Value));
        Assert.Single(responseRepo.GetSubmissionsBySession(_sessionId));
    }

    [Fact]
    public async Task SubmitSurvey_MissingAnswer_ReturnsBadRequestWithoutWritingAnything()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questionRepo = CreateQuestions(database);
        var responseRepo = new ResponseRepository(database);
        var incomplete = CreateValidRequest() with
        {
            Answers = CreateValidRequest().Answers.Take(3).ToList()
        };

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            incomplete,
            CreateSessionRepository(SessionStatus.Active).Object,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
        Assert.Empty(responseRepo.GetBySessionAndDevice(_sessionId, _deviceId));
        Assert.Empty(responseRepo.GetSubmissionsBySession(_sessionId));
    }

    [Fact]
    public async Task SubmitSurvey_InvalidOption_ReturnsBadRequestWithoutWritingAnything()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questionRepo = CreateQuestions(database);
        var responseRepo = new ResponseRepository(database);
        var invalid = CreateValidRequest() with
        {
            Answers =
            [
                new SurveyAnswerRequest(QuestionId(0), ["Not an option"]),
                .. CreateValidRequest().Answers.Skip(1)
            ]
        };

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            invalid,
            CreateSessionRepository(SessionStatus.Active).Object,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
        Assert.Empty(responseRepo.GetBySessionAndDevice(_sessionId, _deviceId));
    }

    [Theory]
    [InlineData(SessionStatus.Draft)]
    [InlineData(SessionStatus.Closed)]
    public async Task SubmitSurvey_InactiveSession_ReturnsConflict(SessionStatus status)
    {
        using var database = new LiteDatabase("Filename=:memory:");

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(
            _sessionId,
            CreateValidRequest(),
            CreateSessionRepository(status).Object,
            CreateQuestions(database),
            new ResponseRepository(database),
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<string>>(result);
    }

    private Mock<ISessionRepository> CreateSessionRepository(SessionStatus status)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(repo => repo.GetByIdAsync(_sessionId))
            .ReturnsAsync(new Session { Id = _sessionId, Status = status });
        return repository;
    }

    private QuestionRepository CreateQuestions(LiteDatabase database)
    {
        database.GetCollection<Session>("sessions").Insert(new Session { Id = _sessionId, Status = SessionStatus.Active });
        var repository = new QuestionRepository(database);
        repository.Insert(new Question
        {
            Id = QuestionId(0),
            SessionId = _sessionId,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        });
        repository.Insert(new Question
        {
            Id = QuestionId(1),
            SessionId = _sessionId,
            Text = "Choose all",
            Type = QuestionType.MultipleAnswer,
            Options = ["A", "B", "C"]
        });
        repository.Insert(new Question
        {
            Id = QuestionId(2),
            SessionId = _sessionId,
            Text = "True or false",
            Type = QuestionType.TrueFalse
        });
        repository.Insert(new Question
        {
            Id = QuestionId(3),
            SessionId = _sessionId,
            Text = "Match",
            Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = "HTTP", Match = "Protocol" },
                new MatchingPair { Prompt = "HTML", Match = "Markup" }
            ]
        });
        return repository;
    }

    private SubmitSurveyRequest CreateValidRequest() => new(
        _deviceId,
        [
            new SurveyAnswerRequest(QuestionId(0), ["A"]),
            new SurveyAnswerRequest(QuestionId(1), ["A", "C"]),
            new SurveyAnswerRequest(QuestionId(2), ["True"]),
            new SurveyAnswerRequest(QuestionId(3), ["Protocol", "Markup"])
        ]);

    private static Guid QuestionId(int index) =>
        Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
}
