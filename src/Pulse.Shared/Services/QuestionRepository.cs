using LiteDB;
using Pulse.Domain.Entities;

namespace Pulse.Common.Services;

public class QuestionRepository
{
    private readonly ILiteCollection<Question> _col;

    public QuestionRepository(LiteDatabase db)
    {
        _col = db.GetCollection<Question>("questions");
    }

    public IEnumerable<Question> GetAll() => _col.FindAll();
    public Question Insert(Question q) { _col.Insert(q); return q; }
    public Question? GetById(Guid id) => _col.FindById(id);
    public bool Update(Question q) => _col.Update(q);
    public bool Delete(Guid id) => _col.Delete(id);
    public IEnumerable<Question> GetBySessionId(Guid sessionId) => _col.Find(q => q.SessionId == sessionId);
}
