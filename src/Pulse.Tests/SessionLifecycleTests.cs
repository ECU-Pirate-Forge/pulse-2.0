using LiteDB;
using Pulse.Application.Services;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class SessionLifecycleTests
{
    [Theory]
    [InlineData(SessionStatus.Draft, SessionStatus.Active, true)]
    [InlineData(SessionStatus.Active, SessionStatus.Closed, true)]
    [InlineData(SessionStatus.Draft, SessionStatus.Closed, false)]
    [InlineData(SessionStatus.Active, SessionStatus.Draft, false)]
    [InlineData(SessionStatus.Closed, SessionStatus.Active, false)]
    public void CanTransition_ReturnsExpectedResult(
        SessionStatus current,
        SessionStatus next,
        bool expected)
    {
        Assert.Equal(expected, SessionLifecycleService.CanTransition(current, next));
    }

    [Fact]
    public void NewSession_DefaultsToDraftSurveyWithBlindedResults()
    {
        var session = new Session();

        Assert.Equal(SessionStatus.Draft, session.Status);
        Assert.Equal(SessionMode.Survey, session.Mode);
        Assert.Equal(ResultsVisibility.Blinded, session.ResultsVisibility);
    }

    [Fact]
    public void ExistingLiteDbDocument_WithStringStatusAndNoMode_UsesSafeDefaults()
    {
        using var database = new LiteDatabase(new MemoryStream());
        var collection = database.GetCollection<BsonDocument>("sessions");
        var id = Guid.NewGuid();
        collection.Insert(new BsonDocument
        {
            ["_id"] = id,
            ["Status"] = "Active",
            ["IsUnblinded"] = true
        });

        var session = database.GetCollection<Session>("sessions").FindById(id);

        Assert.NotNull(session);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(SessionMode.Survey, session.Mode);
        Assert.Equal(ResultsVisibility.Revealed, session.ResultsVisibility);
    }

    [Fact]
    public void UnknownStoredStatus_FallsBackToDraft()
    {
        var session = new Session
        {
            StoredStatus = "Unexpected"
        };

        Assert.Equal(SessionStatus.Draft, session.Status);
    }
}
