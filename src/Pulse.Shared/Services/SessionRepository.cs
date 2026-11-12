using LiteDB;
using System.Collections.Concurrent;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.Common.Services;
using Pulse.Shared.Services;

namespace Pulse.Common.Services;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private readonly IJoinCodeGenerator _joinCodeGenerator;
    private const string SessionCollectionName = "sessions";
    private readonly ILiteCollection<Session> _sessions;
    private readonly ConcurrentDictionary<Guid, Session> _cache = new();

    public SessionRepository(LiteDatabase db, IJoinCodeGenerator joinCodeGenerator)
    {
        _db = db;
        _joinCodeGenerator = joinCodeGenerator;
        _sessions = _db.GetCollection<Session>(SessionCollectionName);
    }

    public Session Insert(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.Title))
        {
            throw new ArgumentException("Session title is required.", nameof(session));
        }

        if (session.Id == Guid.Empty)
        {
            session.Id = Guid.NewGuid();
        }

        if (session.CreatedAt == default)
        {
            session.CreatedAt = DateTime.UtcNow;
        }

        if (session.UpdatedAt == default)
        {
            session.UpdatedAt = session.CreatedAt;
        }

        _sessions.Insert(session);
        _cache[session.Id] = session;
        return session;
    }

    public Session? GetById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var persisted = _sessions.FindOne(s => s.Id == id);
        if (persisted is not null)
        {
            _cache[id] = persisted;
        }

        return persisted;
    }

    public Task<Session?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(GetById(id));
    }

    public Task<Session?> GetByJoinCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return Task.FromResult<Session?>(null);

        var session = _sessions.FindOne(s => s.JoinCode == joinCode);
        return Task.FromResult<Session?>(session);
    }

    public async Task<string> GenerateUniqueJoinCodeAsync()
    {
        string code;
        do
        {
            code = _joinCodeGenerator.Generate();
        } while (await JoinCodeExistsAsync(code));

        return code;
    }

    public Task<IEnumerable<Session>> GetByInstructorCodeAsync(string instructorCode)
    {
        if (string.IsNullOrWhiteSpace(instructorCode))
            return Task.FromResult(Enumerable.Empty<Session>());

        var sessions = _sessions.Find(s => s.InstructorCode == instructorCode).ToList();
        return Task.FromResult<IEnumerable<Session>>(sessions);
    }

    public Task<Session> InsertAsync(Session session, CancellationToken ct = default)
    {
        _sessions.Insert(session);
        _cache[session.Id] = session;
        return Task.FromResult(session);
    }

    public bool Update(Session session)
    {
        var updated = _sessions.Update(session);
        if (updated)
            _cache[session.Id] = session;
        return updated;
    }
    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
    {
        var exists = _sessions.Exists(s => s.JoinCode == joinCode);
        return Task.FromResult(exists);
    }

    /// <summary>Atomically clears a survey run and reopens it without removing its questions.</summary>
    public bool ResetSurvey(Guid id) => ResetSurveyRun(id, SessionStatus.Active, clearResponses: true);

    /// <summary>Atomically pauses a survey for editing after any required response-clearing consent.</summary>
    public bool BeginSurveyEdit(Guid id, bool clearResponses) => ResetSurveyRun(id, SessionStatus.Draft, clearResponses);

    private bool ResetSurveyRun(Guid id, SessionStatus targetStatus, bool clearResponses)
    {
        if (id == Guid.Empty)
            return false;

        if (!_db.BeginTrans())
            throw new InvalidOperationException("Could not start the survey reset transaction.");

        try
        {
            // Acquire the session write lock before responses, matching guarded submission transactions.
            _sessions.UpdateMany("{ Status: $.Status }", Query.EQ("_id", id));
            var session = _sessions.FindById(id);
            if (session is null || session.Mode != SessionMode.Survey)
            {
                _db.Rollback();
                return false;
            }

            var responses = _db.GetCollection<Response>("responses");
            var submissions = _db.GetCollection<SurveySubmission>("survey_submissions");
            if (!clearResponses && (responses.Exists(response => response.SessionId == id) ||
                submissions.Exists(submission => submission.SessionId == id)))
            {
                _db.Rollback();
                return false;
            }

            responses.DeleteMany(response => response.SessionId == id);
            submissions.DeleteMany(submission => submission.SessionId == id);
            session.Status = targetStatus;
            session.ResultsVisibility = ResultsVisibility.Blinded;
            session.SharedQuestionId = null;
            session.RevealedQuestionIds = [];
            session.ResetVersion = checked(session.ResetVersion + 1);
            session.UpdatedAt = DateTime.UtcNow;
            if (!_sessions.Update(session))
                throw new InvalidOperationException("Could not persist the survey reset.");

            if (!_db.Commit())
                throw new InvalidOperationException("Could not commit the survey reset transaction.");
        }
        catch
        {
            _db.Rollback();
            throw;
        }

        _cache.TryRemove(id, out _);
        return true;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
            return Task.FromResult(false);

        if (!_db.BeginTrans())
            throw new InvalidOperationException("Could not start the session deletion transaction.");

        try
        {
            var deleted = _sessions.Delete(id);
            if (!deleted)
            {
                _db.Rollback();
                return Task.FromResult(false);
            }

            _db.GetCollection<Question>("questions").DeleteMany(q => q.SessionId == id);
            _db.GetCollection<Response>("responses").DeleteMany(r => r.SessionId == id);
            _db.GetCollection("survey_grounding").Delete(id);
            _db.GetCollection("survey_generation_previews").DeleteMany(Query.EQ("SessionId", id));

            if (!_db.Commit())
                throw new InvalidOperationException("Could not commit the session deletion transaction.");
        }
        catch
        {
            _db.Rollback();
            throw;
        }

        _cache.TryRemove(id, out _);
        return Task.FromResult(true);
    }
}
