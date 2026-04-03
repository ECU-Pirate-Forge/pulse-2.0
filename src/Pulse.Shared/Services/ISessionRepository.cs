using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface ISessionRepository
{
    Session Insert(Session session);
    Session? GetById(Guid id);

    /// <summary>
    /// Retrieves a session by its join code.
    /// </summary>
    /// <param name="joinCode">The join code to look up</param>
    /// <returns>The session if found; null otherwise</returns>
    Task<Session?> GetByJoinCodeAsync(string joinCode);

    /// <summary>
    /// Generates a unique 6-character alphanumeric join code.
    /// Automatically checks for collisions in the repository.
    /// </summary>
    /// <returns>A unique 6-character join code</returns>
    Task<string> GenerateUniqueJoinCodeAsync();

    /// <summary>
    /// Returns all sessions owned by the given instructor code.
    /// </summary>
    Task<IEnumerable<Session>> GetByInstructorCodeAsync(string instructorCode);

    Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default);
}
