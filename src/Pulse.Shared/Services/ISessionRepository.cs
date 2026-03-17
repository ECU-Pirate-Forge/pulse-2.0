using Pulse.Shared.Models;

public interface ISessionRepository
{

    Session? GetByJoinCode(string joinCode);
}
