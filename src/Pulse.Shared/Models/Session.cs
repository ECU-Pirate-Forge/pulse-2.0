// src/Pulse.Shared/Models/Session.cs

namespace Pulse.Shared.Models;

public class Session
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}