using LiteDB;

namespace Pulse.Shared.Models;

public class SurveySubmission
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
