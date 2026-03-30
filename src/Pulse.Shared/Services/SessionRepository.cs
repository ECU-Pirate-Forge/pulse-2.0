using LiteDB;
using Pulse.Shared.Models;
using Pulse.Common.Services;

namespace Pulse.Common.Services;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private const string SessionCollectionName = "sessions";

    public SessionRepository(LiteDatabase db)
    {
        _db = db;
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
    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
    {
        var collection = _db.GetCollection("sessions");
        var exists = collection.Exists(Query.EQ("JoinCode", joinCode));
        return Task.FromResult(exists);
    }
}
