using LiteDB;
using System.Collections.Concurrent;
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

        if (string.IsNullOrWhiteSpace(session.Status))
        {
            session.Status = "Draft";
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

    public Task DeleteAsync(Guid id)
    {
        _sessions.Delete(id);
        _cache.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
