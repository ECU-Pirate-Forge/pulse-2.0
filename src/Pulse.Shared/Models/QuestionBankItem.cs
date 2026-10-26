using LiteDB;
using Pulse.Domain.Entities;

namespace Pulse.Shared.Models;

public enum QuestionModerationStatus
{
    Approved = 0,
    Pending = 1,
    Rejected = 2
}

public enum QuestionSource
{
    Legacy = 0,
    InstructorAuthored = 1,
    StudentSuggestion = 2,
    CsvImport = 3
}

public enum QuestionBankSort
{
    Newest = 0,
    Oldest = 1,
    Text = 2
}

public sealed record QuestionBankQuery(
    string? Text = null,
    int? Type = null,
    string? Course = null,
    string? Topic = null,
    string? Tag = null,
    QuestionModerationStatus? ModerationStatus = null,
    QuestionSource? Source = null,
    bool? TriviaEligible = null,
    QuestionBankSort Sort = QuestionBankSort.Newest,
    bool? IsDraft = null);

public class QuestionBankItem
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<string> Options { get; set; } = [];
    public List<MatchingPair> MatchingPairs { get; set; } = [];
    public string Course { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool IsDraft { get; set; }
    public QuestionModerationStatus ModerationStatus { get; set; } = QuestionModerationStatus.Approved;
    public QuestionSource Source { get; set; } = QuestionSource.Legacy;
    public string ContributorAttribution { get; set; } = string.Empty;
    public bool TriviaEligible { get; set; }
    public List<string> CorrectAnswers { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceQuote { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
