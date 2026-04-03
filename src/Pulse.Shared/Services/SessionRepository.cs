using LiteDB;
using Pulse.Shared.Models;
using Pulse.Common.Services;
using Pulse.Shared.Services;

namespace Pulse.Common.Services;

public class SessionRepository : ISessionRepository
{
    private readonly LiteDatabase _db;
    private readonly IJoinCodeGenerator _joinCodeGenerator;
    private const string SessionCollectionName = "sessions";

    public SessionRepository(LiteDatabase db, IJoinCodeGenerator joinCodeGenerator)
    {
        _db = db;
        _joinCodeGenerator = joinCodeGenerator;
    }

    public async Task<Session?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
            return null;

        var collection = _db.GetCollection<Session>(SessionCollectionName);
        var session = collection.FindById(id);
        return await Task.FromResult(session);
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
