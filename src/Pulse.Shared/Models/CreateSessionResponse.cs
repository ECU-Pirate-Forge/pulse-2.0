namespace Pulse.Shared.Models;

public class CreateSessionResponse
{
    public Guid Id { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
}
