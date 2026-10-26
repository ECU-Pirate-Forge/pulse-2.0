using LiteDB;
using Pulse.Domain.Entities;
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
        _col.EnsureIndex(x => x.Course);
        _col.EnsureIndex(x => x.Topic);
        _col.EnsureIndex(x => x.ModerationStatus);
    }

    public QuestionBankItem Insert(QuestionBankItem item)
    {
        CopyStructuredData(item);
        _col.Insert(item);
        return item;
    }

    public IEnumerable<QuestionBankItem> GetAll() => _col.FindAll();

    public QuestionBankItem? GetById(Guid id) => _col.FindById(id);

    public bool Update(QuestionBankItem item)
    {
        CopyStructuredData(item);
        return _col.Update(item);
    }

    public bool Delete(Guid id) => _col.Delete(id);

    public IEnumerable<QuestionBankItem> Search(string? text = null, int? type = null)
        => Search(new QuestionBankQuery(Text: text, Type: type));

    public IEnumerable<QuestionBankItem> Search(QuestionBankQuery query)
    {
        var all = _col.FindAll();

        if (!string.IsNullOrWhiteSpace(query.Text))
            all = all.Where(x => x.Text.Contains(query.Text, StringComparison.OrdinalIgnoreCase));

        if (query.Type is not null)
            all = all.Where(x => (int)x.Type == query.Type.Value);

        if (!string.IsNullOrWhiteSpace(query.Course))
            all = all.Where(x => string.Equals(x.Course, query.Course, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Topic))
            all = all.Where(x => string.Equals(x.Topic, query.Topic, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Tag))
            all = all.Where(x => x.Tags.Any(tag =>
                string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase)));

        if (query.ModerationStatus is not null)
            all = all.Where(x => x.ModerationStatus == query.ModerationStatus);

        if (query.IsDraft is not null)
            all = all.Where(x => x.IsDraft == query.IsDraft);
        else if (query.ModerationStatus == QuestionModerationStatus.Approved)
            all = all.Where(x => !x.IsDraft);

        if (query.Source is not null)
            all = all.Where(x => x.Source == query.Source);

        if (query.TriviaEligible is not null)
            all = all.Where(x => x.TriviaEligible == query.TriviaEligible);

        return query.Sort switch
        {
            QuestionBankSort.Oldest => all.OrderBy(x => x.CreatedAt),
            QuestionBankSort.Text => all.OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase),
            _ => all.OrderByDescending(x => x.CreatedAt)
        };
    }

    private static void CopyStructuredData(QuestionBankItem item)
    {
        item.Options = new List<string>(item.Options ?? []);
        item.MatchingPairs = (item.MatchingPairs ?? [])
            .Select(pair => new MatchingPair { Prompt = pair.Prompt, Match = pair.Match })
            .ToList();
        item.Tags = (item.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        item.CorrectAnswers = new List<string>(item.CorrectAnswers ?? []);
    }
}
