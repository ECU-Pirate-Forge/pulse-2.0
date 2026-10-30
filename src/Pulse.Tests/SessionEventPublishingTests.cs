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
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class SessionEventPublishingTests
{
    [Fact]
    public async Task SubmitSurvey_PublishesUpdatedUniqueCounts()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessionId = Guid.NewGuid();
        var question = new Question
        {
            SessionId = sessionId,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        };
        var questionRepo = new QuestionRepository(database);
        questionRepo.Insert(question);
        var responseRepo = new ResponseRepository(database);
        database.GetCollection<Session>("sessions").Insert(new Session { Id = sessionId, Status = SessionStatus.Active });
        var sessionRepo = SessionRepository(new Session
        {
            Id = sessionId,
            Status = SessionStatus.Active
        });
        var publisher = new Mock<ISessionEventPublisher>();

        await SurveySubmissionEndpointHandlers.SubmitSurvey(
            sessionId,
            new SubmitSurveyRequest(
                Guid.NewGuid().ToString(),
                [new SurveyAnswerRequest(question.Id, ["A"])]),
            sessionRepo.Object,
            questionRepo,
            responseRepo,
            new DeviceIdValidationService(),
            LoggerFactory.Create(_ => { }),
            new DefaultHttpContext(),
            publisher.Object);

        publisher.Verify(item => item.PublishSubmissionUpdatedAsync(
            sessionId,
            It.Is<SessionProgressUpdate>(update =>
                update.ParticipantCount == 1 &&
                update.CompletedCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnblindSession_PublishesResultsRevealed()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            InstructorCode = "INST",
            Status = SessionStatus.Closed
        };
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST";
        var publisher = new Mock<ISessionEventPublisher>();

        await SessionEndpointHandlers.UnblindSession(
            session.Id,
            context,
            SessionRepository(session).Object,
            publisher.Object);

        publisher.Verify(item => item.PublishResultsRevealedAsync(
            session.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseSession_PublishesSessionClosed()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            InstructorCode = "INST",
            Status = SessionStatus.Active
        };
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST";
        var publisher = new Mock<ISessionEventPublisher>();

        await SessionEndpointHandlers.CloseSession(
            session.Id,
            context,
            SessionRepository(session).Object,
            publisher.Object);

        publisher.Verify(item => item.PublishSessionClosedAsync(
            session.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ISessionRepository> SessionRepository(Session session)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(repo => repo.GetByIdAsync(session.Id)).ReturnsAsync(session);
        repository.Setup(repo => repo.Update(It.IsAny<Session>())).Returns(true);
        return repository;
    }
}
