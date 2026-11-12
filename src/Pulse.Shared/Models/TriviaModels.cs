using LiteDB;
using System.Text.Json.Serialization;
using Pulse.Domain.Entities;

namespace Pulse.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriviaCompetitionMode
{
    Individual = 0,
    Team = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriviaGameStatus
{
    Lobby = 0,
    QuestionOpen = 1,
    QuestionClosed = 2,
    Finished = 3
}

public sealed class TriviaGame
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public TriviaCompetitionMode CompetitionMode { get; set; }
    public TriviaGameStatus Status { get; set; } = TriviaGameStatus.Lobby;
    public int PointsPerCorrectAnswer { get; set; } = 100;
    public int MaximumTimeBonus { get; set; } = 50;
    public int QuestionDurationSeconds { get; set; } = 20;
    public Guid? CurrentQuestionId { get; set; }
    public DateTime? QuestionOpenedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TriviaQuestion
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid QuestionBankItemId { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<string> Options { get; set; } = [];
    public List<string> CorrectAnswers { get; set; } = [];
    public int SortOrder { get; set; }
}

public sealed class TriviaTeam
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class TriviaParticipant
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? TeamId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    [JsonIgnore]
    public string GameDeviceKey { get; set; } = string.Empty;
}

public sealed class TriviaAnswer
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid ParticipantId { get; set; }
    public List<string> Values { get; set; } = [];
    public bool IsCorrect { get; set; }
    public int AwardedPoints { get; set; }
    public DateTime SubmittedAt { get; set; }
    [JsonIgnore]
    public string QuestionParticipantKey { get; set; } = string.Empty;
}

public sealed record TriviaScoreResult(bool Accepted, bool IsCorrect, int Points);

public sealed record TriviaLeaderboardEntry(int Rank, string Name, int Score);
