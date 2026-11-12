using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Resources;
using Pulse.Shared.Services;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class SurveyResetTests : IDisposable
{
    private readonly LiteDatabase database = new("Filename=:memory:", new BsonMapper());
    private readonly SessionRepository sessions;
    private readonly QuestionRepository questions;
    private readonly ResponseRepository responses;
    private readonly Session session;
    private readonly Question question;
    private readonly Guid otherSessionId = Guid.NewGuid();
    private readonly string deviceId = Guid.NewGuid().ToString();

    public SurveyResetTests()
    {
        sessions = new SessionRepository(database, new JoinCodeGenerator());
        questions = new QuestionRepository(database);
        responses = new ResponseRepository(database);
        session = sessions.Insert(new Session
        {
            Title = "Reusable survey", InstructorCode = "OWNER", JoinCode = "ABC123",
            Status = SessionStatus.Closed, ResultsVisibility = ResultsVisibility.Revealed
        });
        question = questions.Insert(new Question
        {
            SessionId = session.Id, Text = "Choose one", Type = QuestionType.MultipleChoice,
            Options = ["A", "B"], CorrectAnswers = ["B"], Explanation = "B is supported by the source."
        });
        responses.SubmitSurvey(
            [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"A\"]" }],
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId, SubmittedAt = DateTime.UtcNow });
        responses.SubmitSurvey(
            [new Response { SessionId = otherSessionId, QuestionId = Guid.NewGuid(), DeviceId = deviceId, Value = "[\"B\"]" }],
            new SurveySubmission { SessionId = otherSessionId, DeviceId = deviceId, SubmittedAt = DateTime.UtcNow });
    }

    [Fact]
    public void Reset_ClearsResponses()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Empty(responses.GetBySession(session.Id));
    }

    [Fact]
    public void Reset_ClearsCompletionRecords()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Empty(responses.GetSubmissionsBySession(session.Id));
    }

    [Fact]
    public void Reset_PreservesQuestionsAndAnswerKeys()
    {
        var original = JsonSerializer.Serialize(questions.GetBySessionId(session.Id));

        sessions.ResetSurvey(session.Id);

        Assert.Equal(original, JsonSerializer.Serialize(questions.GetBySessionId(session.Id)));
    }

    [Fact]
    public void Reset_PreservesJoinCode()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Equal("ABC123", sessions.GetById(session.Id)?.JoinCode);
    }

    [Fact]
    public void Reset_ReopensSurvey()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Equal(SessionStatus.Active, sessions.GetById(session.Id)?.Status);
    }

    [Fact]
    public void Reset_BlindsResults()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Equal(ResultsVisibility.Blinded, sessions.GetById(session.Id)?.ResultsVisibility);
    }

    [Fact]
    public void Reset_ClearsQuestionSharingAndRevealState()
    {
        session.SharedQuestionId = question.Id;
        session.RevealedQuestionIds.Add(question.Id);
        sessions.Update(session);

        sessions.ResetSurvey(session.Id);

        var reset = sessions.GetById(session.Id)!;
        Assert.Null(reset.SharedQuestionId);
        Assert.Empty(reset.RevealedQuestionIds);
    }

    [Fact]
    public void Reset_IncrementsPersistedVersionOnEveryRun()
    {
        sessions.ResetSurvey(session.Id);
        sessions.ResetSurvey(session.Id);
        var reloaded = new SessionRepository(database, new JoinCodeGenerator());

        Assert.Equal(2, reloaded.GetById(session.Id)?.ResetVersion);
    }

    [Fact]
    public void Reset_PreservesOtherSurveyResponses()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Single(responses.GetBySession(otherSessionId));
    }

    [Fact]
    public void Reset_PreservesOtherSurveyCompletions()
    {
        sessions.ResetSurvey(session.Id);

        Assert.Single(responses.GetSubmissionsBySession(otherSessionId));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("OTHER", 403)]
    [InlineData("OWNER", 204)]
    public async Task ResetEndpoint_RequiresOwningInstructor(string? code, int expectedStatus)
    {
        var result = await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context(code), sessions);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("OTHER")]
    public async Task ResetEndpoint_UnauthorizedRequestPreservesResponses(string? code)
    {
        await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context(code), sessions);

        Assert.Single(responses.GetBySession(session.Id));
    }

    [Fact]
    public async Task ResetEndpoint_UnknownSessionReturnsNotFound()
    {
        var result = await SessionEndpointHandlers.ResetSurveyAsync(Guid.NewGuid(), Context("OWNER"), sessions);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ResetEndpoint_RejectsTrivia()
    {
        session.Mode = SessionMode.Trivia;
        sessions.Update(session);

        var result = await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context("OWNER"), sessions);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ResetEndpoint_AllowsSameDeviceToSubmitAgain()
    {
        await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context("OWNER"), sessions);

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(
            session.Id, new SubmitSurveyRequest(deviceId, [new SurveyAnswerRequest(question.Id, ["B"])]),
            sessions, questions, responses, new DeviceIdValidationService(), NullLoggerFactory.Instance, Context(null));

        Assert.IsType<Ok<SubmitSurveyResponse>>(result);
    }

    [Fact]
    public async Task ResetEndpoint_HidesPreviouslyRevealedAnswerKeys()
    {
        await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context("OWNER"), sessions);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.IsType<Ok<StudentResultsStatusResponse>>(result);
    }

    [Fact]
    public async Task ResetEndpoint_ReportsNewVersionToParticipants()
    {
        await SessionEndpointHandlers.ResetSurveyAsync(session.Id, Context("OWNER"), sessions);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.Equal(1, Assert.IsType<Ok<StudentResultsStatusResponse>>(result).Value?.ResetVersion);
    }

    [Theory]
    [InlineData(ResultsVisibility.Blinded)]
    [InlineData(ResultsVisibility.Revealed)]
    public async Task InstructorResults_IncludeUnselectedCorrectAnswers(ResultsVisibility visibility)
    {
        session.ResultsVisibility = visibility;
        sessions.Update(session);

        var result = await SessionResultsEndpointHandlers.GetSessionResults(session.Id, Context("OWNER"), sessions, questions, responses);

        Assert.Equal(["B"], Assert.IsType<Ok<InstructorSurveyResultsResponse>>(result).Value!.Questions.Single().CorrectAnswers);
    }

    [Fact]
    public async Task RevealedResults_IncludeUnselectedCorrectAnswers()
    {
        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.Equal(["B"], Assert.IsType<Ok<StudentSurveyResultsResponse>>(result).Value!.Questions.Single().CorrectAnswers);
    }

    [Fact]
    public async Task BlindedResults_DoNotDiscloseAnswerKeys()
    {
        session.ResultsVisibility = ResultsVisibility.Blinded;
        sessions.Update(session);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.DoesNotContain("correctAnswers", JsonSerializer.Serialize(Assert.IsType<Ok<StudentResultsStatusResponse>>(result).Value));
    }

    [Fact]
    public async Task RevealedResults_IncludeMatchingAnswerKeys()
    {
        question.Type = QuestionType.Matching;
        question.MatchingPairs = [new MatchingPair { Prompt = "Term", Match = "Definition" }];
        questions.Update(question);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.Equal(["Term: Definition"], Assert.IsType<Ok<StudentSurveyResultsResponse>>(result).Value!.Questions.Single().CorrectAnswers);
    }

    [Fact]
    public void SurveyText_LoadsLocalizedResetConfirmation()
    {
        Assert.Contains("Questions, answer keys, and the join code will be kept", SurveyText.ResetConfirmation);
    }

    private static DefaultHttpContext Context(string? code)
    {
        var context = new DefaultHttpContext();
        context.Items["InstructorCode"] = code;
        return context;
    }

    public void Dispose() => database.Dispose();
}
