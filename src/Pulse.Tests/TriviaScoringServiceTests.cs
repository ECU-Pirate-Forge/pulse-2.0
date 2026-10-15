using Pulse.Application.Services;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class TriviaScoringServiceTests
{
    private readonly TriviaScoringService _service = new();
    private readonly DateTime _openedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CorrectAnswerAtOpening_EarnsBaseAndMaximumBonus()
    {
        var result = _service.Score(
            ["A"],
            ["A"],
            _openedAt,
            _openedAt,
            TimeSpan.FromSeconds(20),
            100,
            50);

        Assert.True(result.Accepted);
        Assert.True(result.IsCorrect);
        Assert.Equal(150, result.Points);
    }

    [Fact]
    public void CorrectAnswerAtDeadline_EarnsBasePointsOnly()
    {
        var result = _service.Score(
            ["A"],
            ["A"],
            _openedAt,
            _openedAt.AddSeconds(20),
            TimeSpan.FromSeconds(20),
            100,
            50);

        Assert.True(result.Accepted);
        Assert.Equal(100, result.Points);
    }

    [Fact]
    public void LateAnswer_IsRejected()
    {
        var result = _service.Score(
            ["A"],
            ["A"],
            _openedAt,
            _openedAt.AddMilliseconds(20_001),
            TimeSpan.FromSeconds(20),
            100,
            50);

        Assert.False(result.Accepted);
        Assert.Equal(0, result.Points);
    }

    [Fact]
    public void SubmittedOrder_DoesNotChangeMultipleAnswerCorrectness()
    {
        var result = _service.Score(
            ["A", "C"],
            ["C", "A"],
            _openedAt,
            _openedAt.AddSeconds(10),
            TimeSpan.FromSeconds(20),
            100,
            50);

        Assert.True(result.IsCorrect);
        Assert.Equal(125, result.Points);
    }

    [Fact]
    public void Leaderboard_PreservesTiesAndAggregatesTeams()
    {
        var teamId = Guid.NewGuid();
        var participants = new[]
        {
            new TriviaParticipant { Id = Guid.NewGuid(), DisplayName = "Ada", TeamId = teamId },
            new TriviaParticipant { Id = Guid.NewGuid(), DisplayName = "Grace", TeamId = teamId },
            new TriviaParticipant { Id = Guid.NewGuid(), DisplayName = "Linus" }
        };
        var teams = new[] { new TriviaTeam { Id = teamId, Name = "Purple" } };
        var answers = new[]
        {
            new TriviaAnswer { ParticipantId = participants[0].Id, AwardedPoints = 100 },
            new TriviaAnswer { ParticipantId = participants[0].Id, AwardedPoints = 100 },
            new TriviaAnswer { ParticipantId = participants[2].Id, AwardedPoints = 200 }
        };

        var individual = _service.BuildIndividualLeaderboard(participants, answers);
        var team = _service.BuildTeamLeaderboard(teams, participants, answers);

        Assert.Equal(1, individual[0].Rank);
        Assert.Equal(200, individual[0].Score);
        Assert.Equal(1, individual[1].Rank);
        Assert.Equal(200, individual[1].Score);
        Assert.Equal(200, Assert.Single(team).Score);
    }
}
