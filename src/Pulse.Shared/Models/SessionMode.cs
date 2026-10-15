using System.Text.Json.Serialization;

namespace Pulse.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionMode
{
    Survey = 0,
    Trivia = 1
}
