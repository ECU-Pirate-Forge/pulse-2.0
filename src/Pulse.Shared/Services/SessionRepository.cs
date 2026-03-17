using LiteDB;
using Pulse.Shared.Models;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private static readonly Random _random = new Random();
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public SessionRepository(LiteDatabase db) => _db = db;

    public string GenerateJoinCode()
    {
        var sessions = _db.GetCollection<Session>("sessions");
        string code;
        do
        {
            code = GenerateCode();
        } while (sessions.FindOne(s => s.JoinCode == code) != null);
        return code;
    }

    public Session? GetByJoinCode(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return null;

        try
        {
            return _db.GetCollection<Session>("sessions").FindOne(s => s.JoinCode == joinCode);
        }
        catch
        {
            return null;
        }
    }

    private string GenerateCode()
    {
        var code = new char[6];
        lock (_random)
        {
            for (int i = 0; i < 6; i++)
                code[i] = Chars[_random.Next(Chars.Length)];
        }
        return new string(code);
    }
}

