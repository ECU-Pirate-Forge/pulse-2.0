namespace Pulse.Common.Services;

public interface ISessionRepository
{
    Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default);
}
