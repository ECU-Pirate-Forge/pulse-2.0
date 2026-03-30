using LiteDB;
using Pulse.Common.Services;

namespace Pulse.Common.Services;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;

    public SessionRepository(LiteDatabase db)
    {
        _db = db;
    }

    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
    {
        var collection = _db.GetCollection("sessions");
        var exists = collection.Exists(Query.EQ("JoinCode", joinCode));
        return Task.FromResult(exists);
    }
}
