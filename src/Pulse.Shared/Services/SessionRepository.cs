using LiteDB;
using Pulse.Shared.Models;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Session> _sessions;

    public SessionRepository(LiteDatabase db)
    {
        _db = db;
        _sessions = _db.GetCollection<Session>("sessions");
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
}
