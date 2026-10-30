using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Pulse.Tests.Tests;

public class SurveyEditingTests : IDisposable
{
    private readonly LiteDatabase database = new("Filename=:memory:", new BsonMapper());
    private readonly SessionRepository sessions;
    private readonly QuestionRepository questions;
    private readonly ResponseRepository responses;
    private readonly Session session;
    private readonly Question question;
    private readonly string deviceId = Guid.NewGuid().ToString();

    public SurveyEditingTests()
    {
        sessions = new SessionRepository(database, new JoinCodeGenerator());
        questions = new QuestionRepository(database);
        responses = new ResponseRepository(database);
        session = sessions.Insert(new Session
        {
            Title = "Editable survey", InstructorCode = "OWNER", JoinCode = "ABC123",
            Status = SessionStatus.Active, ResultsVisibility = ResultsVisibility.Revealed
        });
        question = questions.Insert(new Question
        {
            SessionId = session.Id, Text = "Original question", Type = QuestionType.MultipleChoice,
            Options = ["A", "B"], CorrectAnswers = ["B"]
        });
    }

    [Theory]
    [InlineData(SessionStatus.Draft)]
    [InlineData(SessionStatus.Active)]
    [InlineData(SessionStatus.Closed)]
    public void BeginEdit_AcceptsEveryLifecycleState(SessionStatus status)
    {
        session.Status = status;
        sessions.Update(session);

        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.Equal(SessionStatus.Draft, sessions.GetById(session.Id)?.Status);
    }

    [Fact]
    public void BeginEdit_WithoutConsentLeavesEntireRunUnchanged()
    {
        AddSubmission();
        var original = RunSnapshot();

        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.Equal(original, RunSnapshot());
    }

    [Fact]
    public void BeginEdit_CompletionRecordAloneRequiresConsent()
    {
        database.GetCollection<SurveySubmission>("survey_submissions").Insert(
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId });

        var result = sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.False(result);
    }

    [Fact]
    public void BeginEdit_ConfirmedClearsResponses()
    {
        AddSubmission();

        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        Assert.Empty(responses.GetBySession(session.Id));
    }

    [Fact]
    public void BeginEdit_ConfirmedClearsCompletionRecords()
    {
        AddSubmission();

        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        Assert.Empty(responses.GetSubmissionsBySession(session.Id));
    }

    [Fact]
    public void BeginEdit_PreservesQuestionsAndKeys()
    {
        AddSubmission();
        var original = JsonSerializer.Serialize(questions.GetBySessionId(session.Id));

        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        Assert.Equal(original, JsonSerializer.Serialize(questions.GetBySessionId(session.Id)));
    }

    [Fact]
    public void BeginEdit_PreservesJoinCode()
    {
        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.Equal("ABC123", sessions.GetById(session.Id)?.JoinCode);
    }

    [Fact]
    public void BeginEdit_BlindsPreviouslyRevealedResults()
    {
        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.Equal(ResultsVisibility.Blinded, sessions.GetById(session.Id)?.ResultsVisibility);
    }

    [Fact]
    public void BeginEdit_IncrementsResetVersion()
    {
        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        Assert.Equal(1, sessions.GetById(session.Id)?.ResetVersion);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("OTHER", 403)]
    [InlineData("OWNER", 204)]
    public async Task EditEndpoint_RequiresOwningInstructor(string? code, int expectedStatus)
    {
        var result = await SessionEndpointHandlers.BeginSurveyEditAsync(session.Id, Context(code), sessions);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EditEndpoint_WithResponsesRequiresConfirmation()
    {
        AddSubmission();

        var result = await SessionEndpointHandlers.BeginSurveyEditAsync(session.Id, Context("OWNER"), sessions);

        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EditEndpoint_WithExplicitConsentOpensEditing()
    {
        AddSubmission();

        var result = await SessionEndpointHandlers.BeginSurveyEditAsync(session.Id, Context("OWNER"), sessions, clearResponses: true);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task EditEndpoint_UnknownSessionReturnsNotFound()
    {
        var result = await SessionEndpointHandlers.BeginSurveyEditAsync(Guid.NewGuid(), Context("OWNER"), sessions);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task EditEndpoint_RejectsTrivia()
    {
        session.Mode = SessionMode.Trivia;
        sessions.Update(session);

        var result = await SessionEndpointHandlers.BeginSurveyEditAsync(session.Id, Context("OWNER"), sessions);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EditingSurvey_ReportsPausedStatusWithoutAnswerKeys()
    {
        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(session.Id, sessions, questions, responses);

        Assert.Equal(SessionStatus.Draft, Assert.IsType<Ok<StudentResultsStatusResponse>>(result).Value?.Status);
    }

    [Fact]
    public async Task EditingSurvey_RejectsSubmissions()
    {
        sessions.BeginSurveyEdit(session.Id, clearResponses: false);

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(session.Id,
            new SubmitSurveyRequest(deviceId, [new SurveyAnswerRequest(question.Id, ["B"])]),
            sessions, questions, responses, new DeviceIdValidationService(), NullLoggerFactory.Instance, Context(null));

        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(SessionStatus.Active)]
    [InlineData(SessionStatus.Closed)]
    public async Task EditedSurvey_AfterActivationAcceptsUpdatedChoices(SessionStatus initialStatus)
    {
        session.Status = initialStatus;
        sessions.Update(session);
        AddSubmission();
        await SessionEndpointHandlers.BeginSurveyEditAsync(session.Id, Context("OWNER"), sessions, clearResponses: true);
        await QuestionEndpointHandlers.UpdateQuestionForInstructor(question.Id,
            new UpdateQuestionRequest
            {
                Text = "Updated question", Type = QuestionType.MultipleChoice,
                Options = ["C", "D"], CorrectAnswers = ["D"]
            }, questions, new QuestionService(), sessions, Context("OWNER"));
        await SessionEndpointHandlers.ActivateSession(session.Id, Context("OWNER"), sessions);

        var result = await SurveySubmissionEndpointHandlers.SubmitSurvey(session.Id,
            new SubmitSurveyRequest(deviceId, [new SurveyAnswerRequest(question.Id, ["D"])]),
            sessions, questions, responses, new DeviceIdValidationService(), NullLoggerFactory.Instance, Context(null));

        Assert.IsType<Ok<SubmitSurveyResponse>>(result);
    }

    [Fact]
    public void PausedSurvey_RejectsAlreadyValidatedSubmission()
    {
        var originalVersion = session.ResetVersion;
        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        var saved = responses.TrySubmitSurvey(
            [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"B\"]" }],
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId }, originalVersion);

        Assert.False(saved);
    }

    [Fact]
    public async Task ReactivatedSurvey_RejectsSubmissionFromPreviousRun()
    {
        var originalVersion = session.ResetVersion;
        sessions.BeginSurveyEdit(session.Id, clearResponses: true);
        await SessionEndpointHandlers.ActivateSession(session.Id, Context("OWNER"), sessions);

        var saved = responses.TrySubmitSurvey(
            [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"B\"]" }],
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId }, originalVersion);

        Assert.False(saved);
    }

    [Fact]
    public void RejectedSubmission_DoesNotRestoreClearedResponses()
    {
        AddSubmission();
        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        responses.TrySubmitSurvey(
            [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"B\"]" }],
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId }, 0);

        Assert.Empty(responses.GetBySession(session.Id));
    }

    [Fact]
    public void RejectedSubmission_DoesNotRestoreCompletionRecords()
    {
        AddSubmission();
        sessions.BeginSurveyEdit(session.Id, clearResponses: true);

        responses.TrySubmitSurvey(
            [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"B\"]" }],
            new SurveySubmission { SessionId = session.Id, DeviceId = deviceId }, 0);

        Assert.Empty(responses.GetSubmissionsBySession(session.Id));
    }

    private void AddSubmission() => responses.SubmitSurvey(
        [new Response { SessionId = session.Id, QuestionId = question.Id, DeviceId = deviceId, Value = "[\"B\"]" }],
        new SurveySubmission { SessionId = session.Id, DeviceId = deviceId, SubmittedAt = DateTime.UtcNow });

    private string RunSnapshot() => JsonSerializer.Serialize(new
    {
        Session = sessions.GetById(session.Id),
        Responses = responses.GetBySession(session.Id).ToList(),
        Submissions = responses.GetSubmissionsBySession(session.Id).ToList()
    });

    private static DefaultHttpContext Context(string? code)
    {
        var context = new DefaultHttpContext();
        context.Items["InstructorCode"] = code;
        return context;
    }

    public void Dispose() => database.Dispose();
}
