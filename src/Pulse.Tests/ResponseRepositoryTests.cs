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

    [Fact]
    public void Constructor_LegacyDuplicateResponses_KeepsLatestRecord()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var collection = database.GetCollection<Response>("responses");
        var questionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid().ToString();
        collection.Insert(new Response
        {
            QuestionId = questionId,
            SessionId = sessionId,
            DeviceId = deviceId,
            Value = "old",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        collection.Insert(new Response
        {
            QuestionId = questionId,
            SessionId = sessionId,
            DeviceId = deviceId,
            Value = "new",
            SubmittedAt = DateTime.UtcNow
        });

        var repository = new ResponseRepository(database);

        var response = Assert.Single(repository.GetByQuestionId(questionId));
        Assert.Equal("new", response.Value);
    }

    [Fact]
    public void SubmitSurvey_WhenAWriteFails_RollsBackEveryResponse()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new ResponseRepository(database);
        var duplicateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid().ToString();
        var responses = new[]
        {
            new Response
            {
                Id = duplicateId,
                QuestionId = Guid.NewGuid(),
                SessionId = sessionId,
                DeviceId = deviceId,
                Value = "[\"A\"]"
            },
            new Response
            {
                Id = duplicateId,
                QuestionId = Guid.NewGuid(),
                SessionId = sessionId,
                DeviceId = deviceId,
                Value = "[\"B\"]"
            }
        };

        Assert.Throws<LiteException>(() => repository.SubmitSurvey(
            responses,
            new SurveySubmission
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                SubmittedAt = DateTime.UtcNow
            }));

        Assert.Empty(repository.GetBySessionAndDevice(sessionId, deviceId));
        Assert.Empty(repository.GetSubmissionsBySession(sessionId));
    }
}
