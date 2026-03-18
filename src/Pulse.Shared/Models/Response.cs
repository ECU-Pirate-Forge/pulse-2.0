using System;

namespace Pulse.Shared.Models;

public class Response
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string QuestionId { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}