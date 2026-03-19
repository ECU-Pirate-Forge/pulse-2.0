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
}
