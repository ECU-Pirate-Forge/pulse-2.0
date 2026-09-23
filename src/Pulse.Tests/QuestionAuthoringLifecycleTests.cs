using LiteDB;
using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class QuestionAuthoringLifecycleTests
{
    [Fact]
    public async Task CreateQuestion_ActiveSession_ReturnsConflictAndDoesNotPersist()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questions = new QuestionRepository(database);
        var session = CreateSession(SessionStatus.Active);

        var result = await QuestionEndpointHandlers.CreateQuestionForSession(
            session.Id,
            new Question
            {
                Text = "Choose one",
                Type = QuestionType.MultipleChoice,
                Options = ["A", "B"]
            },
            questions,
            new QuestionService(),
            CreateSessionRepository(session).Object,
            CreateContext(session.InstructorCode));

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<string>>(result);
        Assert.Empty(questions.GetBySessionId(session.Id));
    }

    [Fact]
    public async Task CreateQuestion_DraftOwnedSession_PersistsNormalizedMatchingPairs()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questions = new QuestionRepository(database);
        var session = CreateSession(SessionStatus.Draft);

        var result = await QuestionEndpointHandlers.CreateQuestionForSession(
            session.Id,
            new Question
            {
                Text = "Match the terms",
                Type = QuestionType.Matching,
                MatchingPairs =
                [
                    new MatchingPair { Prompt = " HTTP ", Match = " Web protocol " },
                    new MatchingPair { Prompt = " HTML ", Match = " Markup language " }
                ]
            },
            questions,
            new QuestionService(),
            CreateSessionRepository(session).Object,
            CreateContext(session.InstructorCode));

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<Question>>(result);
        Assert.Equal("HTTP", created.Value!.MatchingPairs[0].Prompt);
        Assert.Equal("Markup language", created.Value.MatchingPairs[1].Match);
    }

    [Fact]
    public async Task DeleteQuestion_ClosedSession_ReturnsConflict()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var questions = new QuestionRepository(database);
        var session = CreateSession(SessionStatus.Closed);
        var question = questions.Insert(new Question
        {
            SessionId = session.Id,
            Text = "Keep me",
            Type = QuestionType.TrueFalse
        });

        var result = await QuestionEndpointHandlers.DeleteQuestionForInstructor(
            question.Id,
            questions,
            CreateSessionRepository(session).Object,
            CreateContext(session.InstructorCode));

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<string>>(result);
        Assert.NotNull(questions.GetById(question.Id));
    }

    private static Session CreateSession(SessionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        InstructorCode = "INST",
        Status = status
    };

    private static Mock<ISessionRepository> CreateSessionRepository(Session session)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);
        return repository;
    }

    private static HttpContext CreateContext(string instructorCode)
    {
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }
}
