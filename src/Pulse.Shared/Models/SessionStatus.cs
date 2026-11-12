using System.Text.Json.Serialization;

namespace Pulse.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2
}
