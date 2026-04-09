using LiteDB;
using Pulse.Domain.Entities;

namespace Pulse.Shared.Models;

public class QuestionBankItem
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<string> Options { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
