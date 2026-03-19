using LiteDB;
using Pulse.Common.Models;

namespace Pulse.Common.Services;

public class QuestionRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Question> _col;

    public QuestionRepository(LiteDatabase db)
    {
        _db = db;
        _col = _db.GetCollection<Question>("questions");
    }

    public IEnumerable<Question> GetAll() => _col.FindAll();
    public Question Insert(Question q) { _col.Insert(q); return q; }
}
