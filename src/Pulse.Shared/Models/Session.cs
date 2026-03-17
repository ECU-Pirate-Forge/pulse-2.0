namespace Pulse.Shared.Models;

public class Session
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SessionStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2
}
