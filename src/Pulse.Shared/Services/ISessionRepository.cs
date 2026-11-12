using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface ISessionRepository
{
    Session Insert(Session session);
    Session? GetById(Guid id);

    /// <summary>
    /// Retrieves a session by its id.
    /// </summary>
    /// <param name="id">The session id to look up</param>
    /// <returns>The session if found; null otherwise</returns>
    Task<Session?> GetByIdAsync(Guid id);

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

    /// <summary>
    /// Persists a new session to the database.
    /// </summary>
    Task<Session> InsertAsync(Session session, CancellationToken ct = default);
    bool Update(Session session);

    /// <summary>
    /// Clears survey responses and submissions, then reopens the survey with results hidden.
    /// Questions, answer keys, and the join code are preserved.
    /// </summary>
    bool ResetSurvey(Guid id);

    /// <summary>
    /// Pauses a survey for editing, preserving questions and its join code.
    /// Returns false without changing the survey if responses exist and clearing was not confirmed.
    /// </summary>
    bool BeginSurveyEdit(Guid id, bool clearResponses);

    /// <summary>
    /// Deletes a session by its id.
    /// </summary>
    /// <param name="id">The session id to delete</param>
    /// <returns>True when the session was deleted; otherwise false.</returns>
    Task<bool> DeleteAsync(Guid id);
}
