using LiteDB;
using Pulse.Shared.Models;
using Pulse.Shared.Models;
using Pulse.Shared.Repositories;

namespace Pulse.Shared.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly ILiteDatabase _db;
    private ILiteCollection<Session> Sessions => _db.GetCollection<Session>("sessions");

    public SessionRepository(ILiteDatabase db)
    {
        _db = db;
        Sessions.EnsureIndex(x => x.JoinCode, unique: true);
    }

    public Task InsertAsync(Session session, CancellationToken ct = default)
    {
        Sessions.Insert(session);
        return Task.CompletedTask;
    }

    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
    {
        var exists = Sessions.Exists(x => x.JoinCode == joinCode);
        return Task.FromResult(exists);
    }
}