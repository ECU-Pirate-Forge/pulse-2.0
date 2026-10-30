using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Shared.Contracts.V1;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class SessionResultsEndpointTests
{
    private Mock<ISessionRepository> BuildSessionRepo(Session? session = null)
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(session);
        return repo;
    }

    private Mock<IResponseRepository> BuildResponseRepo(List<Response>? responses = null)
    {
        responses ??= [];
        var repo = new Mock<IResponseRepository>();
        repo.Setup(r => r.GetByQuestionId(It.IsAny<Guid>()))
            .Returns((Guid id) => responses.Where(r => r.QuestionId == id));
        repo.Setup(r => r.GetBySession(It.IsAny<Guid>()))
            .Returns((Guid id) => responses.Where(r => r.SessionId == id));
        repo.Setup(r => r.GetSubmissionsBySession(It.IsAny<Guid>()))
            .Returns([]);
        return repo;
    }

    private HttpContext BuildContext(string? instructorCode = null)
    {
        var context = new DefaultHttpContext();
        if (instructorCode is not null)
            context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }

    [Fact]
    public async Task GetSessionResults_ValidRequest_Returns200WithTallies()
    {
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Title = "Test Session", InstructorCode = "INST001" };

        var sessionRepo = BuildSessionRepo(session);
        var responseRepo = BuildResponseRepo([
            new Response { SessionId = sessionId, QuestionId = questionId, DeviceId = "d1", Value = "A" },
            new Response { SessionId = sessionId, QuestionId = questionId, DeviceId = "d2", Value = "A" },
            new Response { SessionId = sessionId, QuestionId = questionId, DeviceId = "d3", Value = "B" }
        ]);

        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var questionRepo = new QuestionRepository(db);
        questionRepo.Insert(new Question { Id = questionId, SessionId = sessionId, Text = "Q1", Type = QuestionType.MultipleChoice, Options = ["A", "B", "C"], CorrectAnswers = ["C"] });

        var context = BuildContext("INST001");

        var result = await SessionResultsEndpointHandlers.GetSessionResults(
            sessionId, context, sessionRepo.Object, questionRepo, responseRepo.Object);

        var okResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<InstructorSurveyResultsResponse>>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(3, okResult.Value!.ParticipantCount);
        Assert.Single(okResult.Value.Questions);
        var questionTally = okResult.Value.Questions[0];
        Assert.Equal(2, questionTally.Tallies.First(t => t.Value == "A").Count);
        Assert.Equal(1, questionTally.Tallies.First(t => t.Value == "B").Count);
        Assert.Equal(0, questionTally.Tallies.First(t => t.Value == "C").Count);
        Assert.Equal(["A", "B", "C"], questionTally.Tallies.Select(tally => tally.Value));
        Assert.Equal(["C"], questionTally.CorrectAnswers);
    }

    [Fact]
    public async Task GetSessionResults_MissingInstructorCode_Returns401()
    {
        var sessionRepo = BuildSessionRepo();
        var responseRepo = BuildResponseRepo();
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var questionRepo = new QuestionRepository(db);
        var context = BuildContext(null);

        var result = await SessionResultsEndpointHandlers.GetSessionResults(
            Guid.NewGuid(), context, sessionRepo.Object, questionRepo, responseRepo.Object);

        var statusResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(401, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetSessionResults_UnknownSessionId_Returns404()
    {
        var sessionRepo = BuildSessionRepo(null);
        var responseRepo = BuildResponseRepo();
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var questionRepo = new QuestionRepository(db);
        var context = BuildContext("INST001");

        var result = await SessionResultsEndpointHandlers.GetSessionResults(
            Guid.NewGuid(), context, sessionRepo.Object, questionRepo, responseRepo.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task GetSessionResults_WrongInstructorCode_Returns403()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Title = "Test", InstructorCode = "RIGHTCODE" };
        var sessionRepo = BuildSessionRepo(session);
        var responseRepo = BuildResponseRepo();
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var questionRepo = new QuestionRepository(db);
        var context = BuildContext("WRONGCODE");

        var result = await SessionResultsEndpointHandlers.GetSessionResults(
            sessionId, context, sessionRepo.Object, questionRepo, responseRepo.Object);

        var statusResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
