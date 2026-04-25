using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pulse.WebApi;
using LiteDB;

namespace Pulse.Tests.Tests;

public class ResponseEndpointTests
{
    private readonly DeviceIdValidationService _deviceIdService = new();
    private static ILoggerFactory NullLogger() => LoggerFactory.Create(_ => { });
    private static HttpContext EmptyContext() => new DefaultHttpContext();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _questionId = Guid.NewGuid();
    private readonly string _validDeviceId = Guid.NewGuid().ToString();

    private Mock<ISessionRepository> BuildSessionRepo(Session? session = null)
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(session);
        return repo;
    }

    private Mock<IResponseRepository> BuildResponseRepo()
    {
        var repo = new Mock<IResponseRepository>();
        repo.Setup(r => r.Upsert(It.IsAny<Response>()))
            .Returns((Response r) => r);
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
    public async Task Respond_ValidRequest_Returns200WithSubmittedAt()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST" };
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "A" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo(session).Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<RespondResult>>(result);
        Assert.NotEqual(default, ok.Value!.SubmittedAt);
    }

    [Fact]
    public async Task Respond_MissingDeviceId_Returns400()
    {
        var request = new RespondRequest { DeviceId = null, Value = "A" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo().Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task Respond_InvalidDeviceId_Returns400()
    {
        var request = new RespondRequest { DeviceId = "not-a-uuid", Value = "A" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo().Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task Respond_MissingValue_Returns400()
    {
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo().Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task Respond_UnknownSession_Returns404()
    {
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "A" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo(null).Object,
            BuildQuestionRepo(),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound<string>>(result);
    }

    [Fact]
    public async Task Respond_UnknownQuestion_Returns404()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST" };
        var request = new RespondRequest { DeviceId = _validDeviceId, Value = "A" };

        var result = await ResponseEndpointHandlers.Respond(
            _sessionId, _questionId, request,
            BuildSessionRepo(session).Object,
            BuildQuestionRepo(withQuestion: false),
            BuildResponseRepo().Object,
            _deviceIdService,
            NullLogger(),
            EmptyContext());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound<string>>(result);
    }
}
