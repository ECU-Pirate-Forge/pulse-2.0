using LiteDB;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;

    public SessionRepository(LiteDatabase db)
    {
        _db = db;
    }
}
