using System.ComponentModel.DataAnnotations;
using LiteDB;

namespace Pulse.Domain.Entities;

public class Question
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    [Required]
    public required string Text { get; set; }

    public QuestionType Type { get; set; }
    public List<string> Options { get; set; } = new List<string>();
    public List<MatchingPair> MatchingPairs { get; set; } = new List<MatchingPair>();
    public int SortOrder { get; set; }
    public List<string> CorrectAnswers { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceQuote { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
