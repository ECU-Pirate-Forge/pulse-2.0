using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface IQuestionBankRepository
{
    QuestionBankItem Insert(QuestionBankItem item);
    IEnumerable<QuestionBankItem> GetAll();
    QuestionBankItem? GetById(Guid id);
    bool Update(QuestionBankItem item);
    bool Delete(Guid id);
    IEnumerable<QuestionBankItem> Search(string? text = null, int? type = null);
}
