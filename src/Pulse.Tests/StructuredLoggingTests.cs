using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Pulse.Common.Services;
using Pulse.Application.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class StructuredLoggingTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _questionId = Guid.NewGuid();
    private readonly string _validDeviceId = Guid.NewGuid().ToString();
    private readonly DeviceIdValidationService _deviceIdService = new();

    private Mock<ISessionRepository> BuildSessionRepo(Session? session = null)
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        return repo;
    }

    private Mock<IResponseRepository> BuildResponseRepo()
    {
        var repo = new Mock<IResponseRepository>();
        repo.Setup(r => r.Upsert(It.IsAny<Response>())).Returns((Response r) => r);
        return repo;
    }

    private QuestionRepository BuildQuestionRepo(bool withQuestion = true)
    {
        var db = new LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);
        if (withQuestion)
            repo.Insert(new Question { Id = _questionId, SessionId = _sessionId, Text = "Q1", Type = QuestionType.MultipleChoice, Options = ["A", "B"] });
        return repo;
    }

    [Fact]
    public async Task Respond_ValidRequest_EmitsInformationLog()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST" };
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "A" };

        var loggerMock = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo(session).Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            loggerFactory.Object,
            new DefaultHttpContext());

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Response submitted")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Respond_InactiveSession_EmitsWarningLog()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST" };
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "A" };

        var loggerMock = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo(session).Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            loggerFactory.Object,
            new DefaultHttpContext());

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("session not active")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSession_ValidRequest_EmitsInformationLog()
    {
        var sessionRepo = new Mock<ISessionRepository>();
        sessionRepo.Setup(r => r.JoinCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        sessionRepo.Setup(r => r.InsertAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>())).ReturnsAsync((Session s, CancellationToken _) => s);

        var joinCodeGen = new Mock<IJoinCodeGenerator>();
        joinCodeGen.Setup(g => g.Generate()).Returns("ABC123");

        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST001";

        var loggerMock = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        await SessionEndpointHandlers.CreateSession(
            context,
            new CreateSessionRequest("Test Session"),
            sessionRepo.Object,
            joinCodeGen.Object,
            loggerFactory.Object);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Session created")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
