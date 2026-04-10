using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public class QuestionBankRepository : IQuestionBankRepository
{
    private readonly ILiteCollection<QuestionBankItem> _col;

    public QuestionBankRepository(LiteDatabase db)
    {
        _col = db.GetCollection<QuestionBankItem>("questionbank");
        _col.EnsureIndex(x => x.Type);
        _col.EnsureIndex(x => x.Text);
    }

    public QuestionBankItem Insert(QuestionBankItem item)
    {
        _col.Insert(item);
        return item;
    }

    public IEnumerable<QuestionBankItem> GetAll() => _col.FindAll();

    public QuestionBankItem? GetById(Guid id) => _col.FindById(id);

    public bool Update(QuestionBankItem item) => _col.Update(item);

    public bool Delete(Guid id) => _col.Delete(id);

    public IEnumerable<QuestionBankItem> Search(string? text = null, int? type = null)
    {
        var all = _col.FindAll();

        if (!string.IsNullOrWhiteSpace(text))
            all = all.Where(x => x.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

        if (type is not null)
            all = all.Where(x => (int)x.Type == type.Value);

        return all;
    }
}
