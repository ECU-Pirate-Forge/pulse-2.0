using LiteDB;
using Pulse.Shared.Models;
using Pulse.Common.Services;

namespace Pulse.Common.Services;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Session> _sessions;
    private const string SessionCollectionName = "sessions";

    public SessionRepository(LiteDatabase db)
    {
        _db = db;
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
        return session;
    }

    public Session? GetById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return _sessions.FindById(id);
    }

    public async Task<Session?> GetByJoinCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return null;

        var collection = _db.GetCollection<Session>(SessionCollectionName);
        var session = collection.FindOne(s => s.JoinCode == joinCode);
        return await Task.FromResult(session);
    }

    public async Task<string> GenerateUniqueJoinCodeAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var collection = _db.GetCollection<Session>(SessionCollectionName);

        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        } while (collection.FindOne(s => s.JoinCode == code) != null);

        return await Task.FromResult(code);
    }

    public Task<IEnumerable<Session>> GetByInstructorCodeAsync(string instructorCode)
    {
        if (string.IsNullOrWhiteSpace(instructorCode))
            return Task.FromResult(Enumerable.Empty<Session>());

        var collection = _db.GetCollection<Session>(SessionCollectionName);
        var sessions = collection.Find(s => s.InstructorCode == instructorCode).ToList();
        return Task.FromResult<IEnumerable<Session>>(sessions);
    }

    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
    {
        var collection = _db.GetCollection(SessionCollectionName);
        var exists = collection.Exists(Query.EQ("JoinCode", joinCode));
        return Task.FromResult(exists);
    }
}
