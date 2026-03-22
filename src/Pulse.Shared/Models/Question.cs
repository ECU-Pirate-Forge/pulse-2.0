using System.ComponentModel.DataAnnotations;

namespace Pulse.Common.Models;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    [Required]
    public required string Text { get; set; }

    public QuestionType Type { get; set; }
    public List<string> Options { get; set; } = [];
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
