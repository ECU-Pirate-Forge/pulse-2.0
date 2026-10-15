// src/Pulse.Shared/Models/Session.cs

using LiteDB;
using System.Text.Json.Serialization;

namespace Pulse.Shared.Models;

public class Session
{
    [BsonField("Id")]
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;

    [BsonField("Status")]
    [JsonIgnore]
    public string StoredStatus { get; set; } = nameof(SessionStatus.Draft);

    [BsonIgnore]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SessionStatus Status
    {
        get => Enum.TryParse<SessionStatus>(StoredStatus, true, out var status)
            ? status
            : SessionStatus.Draft;
        set => StoredStatus = value.ToString();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SessionMode Mode { get; set; } = SessionMode.Survey;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public bool IsUnblinded { get; set; }

    [BsonIgnore]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResultsVisibility ResultsVisibility
    {
        get => IsUnblinded ? ResultsVisibility.Revealed : ResultsVisibility.Blinded;
        set => IsUnblinded = value == ResultsVisibility.Revealed;
    }
}