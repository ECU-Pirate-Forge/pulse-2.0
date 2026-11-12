using System.Text.Json.Serialization;

namespace Pulse.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResultsVisibility
{
    Blinded = 0,
    Revealed = 1
}
