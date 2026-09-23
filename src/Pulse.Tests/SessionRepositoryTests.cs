using LiteDB;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Common.Services;
using Pulse.Shared.Services;

namespace Pulse.Tests.Tests;

public class SessionRepositoryTests
{
    private SessionRepository CreateRepository()
    {
        var db = new LiteDatabase("Filename=:memory:");
        return new SessionRepository(db, new JoinCodeGenerator());
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WhenSessionExists_ReturnsSession()
    {
        // Arrange
        var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            JoinCode = "ABC123",
            InstructorCode = "INST001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        collection.Insert(session);

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        // Act
        var result = await repo.GetByJoinCodeAsync("ABC123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ABC123", result.JoinCode);
        Assert.Equal(session.Id, result.Id);
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WhenSessionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByJoinCodeAsync("NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WithNullOrEmpty_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act & Assert
        Assert.Null(await repo.GetByJoinCodeAsync(null!));
        Assert.Null(await repo.GetByJoinCodeAsync(""));
        Assert.Null(await repo.GetByJoinCodeAsync("   "));
    }

    [Fact]
    public async Task GenerateUniqueJoinCodeAsync_ReturnsValidCode()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var code = await repo.GenerateUniqueJoinCodeAsync();

        // Assert
        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(code.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public async Task GenerateUniqueJoinCodeAsync_ChecksForCollision()
    {
        // Arrange
        var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            JoinCode = "DUPLICATE",
            InstructorCode = "INST001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        collection.Insert(session);

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        // Act
        var code1 = await repo.GenerateUniqueJoinCodeAsync();
        var code2 = await repo.GenerateUniqueJoinCodeAsync();

        // Assert
        Assert.NotEqual(code1, code2);
        Assert.NotEqual("DUPLICATE", code1);
        Assert.NotEqual("DUPLICATE", code2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSessionQuestionsAndResponses()
    {
        using var db = new LiteDatabase("Filename=:memory:");
        var repo = new SessionRepository(db, new JoinCodeGenerator());
        var deletedSession = repo.Insert(new Session
        {
            Title = "Delete me",
            InstructorCode = "INST001",
            JoinCode = "DELETE"
        });
        var retainedSession = repo.Insert(new Session
        {
            Title = "Keep me",
            InstructorCode = "INST001",
            JoinCode = "RETAIN"
        });

        var questions = db.GetCollection<Question>("questions");
        var deletedQuestion = new Question
        {
            SessionId = deletedSession.Id,
            Text = "Deleted question"
        };
        var retainedQuestion = new Question
        {
            SessionId = retainedSession.Id,
            Text = "Retained question"
        };
        questions.Insert(deletedQuestion);
        questions.Insert(retainedQuestion);

        var responses = db.GetCollection<Response>("responses");
        responses.Insert(new Response
        {
            SessionId = deletedSession.Id,
            QuestionId = deletedQuestion.Id,
            DeviceId = "deleted-device",
            Value = "A"
        });
        responses.Insert(new Response
        {
            SessionId = retainedSession.Id,
            QuestionId = retainedQuestion.Id,
            DeviceId = "retained-device",
            Value = "B"
        });

        var deleted = await repo.DeleteAsync(deletedSession.Id);

        Assert.True(deleted);
        Assert.Null(repo.GetById(deletedSession.Id));
        Assert.Empty(questions.Find(q => q.SessionId == deletedSession.Id));
        Assert.Empty(responses.Find(r => r.SessionId == deletedSession.Id));
        Assert.NotNull(repo.GetById(retainedSession.Id));
        Assert.Single(questions.Find(q => q.SessionId == retainedSession.Id));
        Assert.Single(responses.Find(r => r.SessionId == retainedSession.Id));
    }
}
