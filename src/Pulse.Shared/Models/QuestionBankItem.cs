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
    CsvImport = 3,
    JsonImport = 4
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
    bool? IsDraft = null,
    Guid? ImportBatchId = null,
    string? CorrectAnswer = null,
    int? CorrectAnswerPosition = null,
    string? Option = null)
{
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Text) || Type.HasValue ||
        !string.IsNullOrWhiteSpace(Course) || !string.IsNullOrWhiteSpace(Topic) ||
        !string.IsNullOrWhiteSpace(Tag) || ModerationStatus.HasValue || Source.HasValue ||
        TriviaEligible.HasValue || IsDraft.HasValue || ImportBatchId.HasValue ||
        !string.IsNullOrWhiteSpace(CorrectAnswer) || CorrectAnswerPosition.HasValue ||
        !string.IsNullOrWhiteSpace(Option);

    public string? Validate()
    {
        if (Type.HasValue && !Enum.IsDefined(typeof(QuestionType), Type.Value))
            return "Question type is invalid.";
        if (ModerationStatus.HasValue && !Enum.IsDefined(ModerationStatus.Value) ||
            Source.HasValue && !Enum.IsDefined(Source.Value) || !Enum.IsDefined(Sort))
            return "Question filter value is invalid.";
        if (ImportBatchId == Guid.Empty)
            return "Import batch ID must not be empty.";
        if (CorrectAnswerPosition is < 1)
            return "Correct answer position must be at least 1.";
        return null;
    }

    public string ToQueryString(int page = 1, int pageSize = 20)
    {
        var values = new Dictionary<string, object?>
        {
            ["text"] = Text, ["type"] = Type, ["course"] = Course, ["topic"] = Topic,
            ["tag"] = Tag, ["moderationStatus"] = ModerationStatus, ["source"] = Source,
            ["triviaEligible"] = TriviaEligible?.ToString().ToLowerInvariant(), ["sort"] = Sort,
            ["isDraft"] = IsDraft?.ToString().ToLowerInvariant(),
            ["importBatchId"] = ImportBatchId, ["correctAnswer"] = CorrectAnswer,
            ["correctAnswerPosition"] = CorrectAnswerPosition, ["option"] = Option,
            ["page"] = page, ["pageSize"] = pageSize
        };
        return string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value?.ToString()))
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!.ToString()!)}"));
    }
}

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
    public Guid? ImportBatchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
