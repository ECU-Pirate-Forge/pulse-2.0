using Pulse.Shared.Models;

namespace Pulse.Shared.Repositories;

public interface ISessionRepository
{
    Task InsertAsync(Session session, CancellationToken ct = default);
    Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default);
}