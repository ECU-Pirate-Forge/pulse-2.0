using LiteDB;
using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class SurveyQuestionEndpointTests
{
    [Fact]
    public async Task GetStudentQuestions_ReturnsOnlyTheCurrentlySharedQuestion()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessionRepo = new SessionRepository(database, new JoinCodeGenerator());
        var session = sessionRepo.Insert(new Session { Title = "Staged survey", Status = SessionStatus.Active });
        var questionRepo = new QuestionRepository(database);
        questionRepo.Insert(new Question { SessionId = session.Id, Text = "First", Type = QuestionType.MultipleChoice, Options = ["A", "B"] });
        var shared = questionRepo.Insert(new Question { SessionId = session.Id, Text = "Second", Type = QuestionType.MultipleChoice, Options = ["C", "D"] });
        session.SharedQuestionId = shared.Id;
        sessionRepo.Update(session);

        var result = await SurveyQuestionEndpointHandlers.GetStudentQuestions(
            session.Id, sessionRepo, questionRepo);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<StudentQuestionResponse>>>(result);
        var question = Assert.Single(ok.Value!);
        Assert.Equal(shared.Id, question.Id);
        Assert.Equal("Second", question.Text);
    }

    [Fact]
    public async Task ShareQuestion_RequiresOwnershipAndPublishesQuestionRefresh()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessionRepo = new SessionRepository(database, new JoinCodeGenerator());
        var session = sessionRepo.Insert(new Session
        {
            Title = "Staged survey", InstructorCode = "OWNER", Status = SessionStatus.Active
        });
        var questionRepo = new QuestionRepository(database);
        var question = questionRepo.Insert(new Question
        {
            SessionId = session.Id, Text = "Question", Type = QuestionType.MultipleChoice, Options = ["A", "B"]
        });
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "OWNER";
        var publisher = new Mock<ISessionEventPublisher>();

        var result = await SurveyQuestionEndpointHandlers.ShareQuestion(
            session.Id, question.Id, context, sessionRepo, questionRepo, publisher.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);
        Assert.Equal(question.Id, sessionRepo.GetById(session.Id)!.SharedQuestionId);
        publisher.Verify(item => item.PublishQuestionsUpdatedAsync(
            session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShareQuestion_RejectsAnotherInstructor()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessionRepo = new SessionRepository(database, new JoinCodeGenerator());
        var session = sessionRepo.Insert(new Session
        {
            Title = "Staged survey", InstructorCode = "OWNER", Status = SessionStatus.Active
        });
        var questionRepo = new QuestionRepository(database);
        var question = questionRepo.Insert(new Question
        {
            SessionId = session.Id, Text = "Question", Type = QuestionType.MultipleChoice, Options = ["A", "B"]
        });
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "OTHER";

        var result = await SurveyQuestionEndpointHandlers.ShareQuestion(
            session.Id, question.Id, context, sessionRepo, questionRepo);

        var forbidden = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Null(sessionRepo.GetById(session.Id)!.SharedQuestionId);
    }

    [Fact]
    public async Task RevealQuestion_ExposesOnlyItsResultsToStudents()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessionRepo = new SessionRepository(database, new JoinCodeGenerator());
        var session = sessionRepo.Insert(new Session
        {
            Title = "Staged survey", InstructorCode = "OWNER", Status = SessionStatus.Active
        });
        var questionRepo = new QuestionRepository(database);
        var hidden = questionRepo.Insert(new Question
        {
            SessionId = session.Id, Text = "Hidden", Type = QuestionType.MultipleChoice, Options = ["A", "B"]
        });
        var revealed = questionRepo.Insert(new Question
        {
            SessionId = session.Id, Text = "Revealed", Type = QuestionType.MultipleChoice, Options = ["C", "D"]
        });
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "OWNER";

        var revealResult = await SurveyQuestionEndpointHandlers.RevealQuestion(
            session.Id, revealed.Id, context, sessionRepo, questionRepo);
        var studentResult = await SessionResultsEndpointHandlers.GetStudentResults(
            session.Id, sessionRepo, questionRepo, new ResponseRepository(database));

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(revealResult);
        Assert.Contains(
            revealed.Id,
            new SessionRepository(database, new JoinCodeGenerator()).GetById(session.Id)!.RevealedQuestionIds);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<StudentSurveyResultsResponse>>(studentResult);
        Assert.Equal(revealed.Id, Assert.Single(ok.Value!.Questions).QuestionId);
        Assert.DoesNotContain(ok.Value.Questions, question => question.QuestionId == hidden.Id);
    }
}
