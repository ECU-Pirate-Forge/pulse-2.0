using Pulse.Shared.Models;

namespace Pulse.Shared.Services;

public interface ISessionRepository
{
    Session? GetByJoinCode(string joinCode);
}
