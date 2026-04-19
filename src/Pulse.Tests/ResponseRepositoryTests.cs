using LiteDB;
using Pulse.Common.Services;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class ResponseRepositoryTests
{
    private ResponseRepository CreateRepository()
    {
        var db = new LiteDatabase("Filename=:memory:");
        return new ResponseRepository(db);
    }

    [Fact]
    public void Upsert_NewResponse_CreatesRecord()
    {
        // Arrange
        var repo = CreateRepository();
        var response = new Response
        {
            QuestionId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid().ToString(),
            Value = "A"
        };

        // Act
        var result = repo.Upsert(response);
        var all = repo.GetByQuestionId(response.QuestionId).ToList();

        // Assert
        Assert.Single(all);
        Assert.Equal("A", all[0].Value);
    }

    [Fact]
    public void Upsert_SameDeviceAndQuestion_ReplacesValue()
    {
        // Arrange
        var repo = CreateRepository();
        var questionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid().ToString();

        var first = new Response { QuestionId = questionId, SessionId = Guid.NewGuid(), DeviceId = deviceId, Value = "A" };
        var second = new Response { QuestionId = questionId, SessionId = Guid.NewGuid(), DeviceId = deviceId, Value = "B" };

        // Act
        repo.Upsert(first);
        repo.Upsert(second);
        var all = repo.GetByQuestionId(questionId).ToList();

        // Assert
        Assert.Single(all);
        Assert.Equal("B", all[0].Value);
    }

    [Fact]
    public void Upsert_DifferentDevice_CreatesNewRecord()
    {
        // Arrange
        var repo = CreateRepository();
        var questionId = Guid.NewGuid();

        var first = new Response { QuestionId = questionId, SessionId = Guid.NewGuid(), DeviceId = Guid.NewGuid().ToString(), Value = "A" };
        var second = new Response { QuestionId = questionId, SessionId = Guid.NewGuid(), DeviceId = Guid.NewGuid().ToString(), Value = "B" };

        // Act
        repo.Upsert(first);
        repo.Upsert(second);
        var all = repo.GetByQuestionId(questionId).ToList();

        // Assert
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Upsert_DifferentQuestion_CreatesNewRecord()
    {
        // Arrange
        var repo = CreateRepository();
        var deviceId = Guid.NewGuid().ToString();
        var questionId1 = Guid.NewGuid();
        var questionId2 = Guid.NewGuid();

        var first = new Response { QuestionId = questionId1, SessionId = Guid.NewGuid(), DeviceId = deviceId, Value = "A" };
        var second = new Response { QuestionId = questionId2, SessionId = Guid.NewGuid(), DeviceId = deviceId, Value = "B" };

        // Act
        repo.Upsert(first);
        repo.Upsert(second);

        // Assert
        Assert.Single(repo.GetByQuestionId(questionId1).ToList());
        Assert.Single(repo.GetByQuestionId(questionId2).ToList());
    }
}
