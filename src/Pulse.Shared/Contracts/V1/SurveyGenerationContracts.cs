using Pulse.Domain.Entities;

namespace Pulse.Shared.Contracts.V1;

public sealed record GroundingDocumentResponse(Guid Id, string FileName, int CharacterCount);
public sealed record SurveyGroundingResponse(
    Guid Revision, string AdditionalContext, IReadOnlyList<GroundingDocumentResponse> Documents);
public sealed record SaveGroundingContextRequest(string AdditionalContext);
public sealed record GenerateSurveyQuestionsRequest(int Count);
public sealed record GeneratedSurveyPreviewResponse(
    Guid Id, IReadOnlyList<GeneratedSurveyQuestion> Questions);

public sealed class GeneratedSurveyQuestion
{
    public string Text { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceQuote { get; set; } = string.Empty;
}

public sealed record StudentQuestionResponse(
    Guid Id, Guid SessionId, string Text, QuestionType Type, IReadOnlyList<string> Options,
    IReadOnlyList<MatchingPair> MatchingPairs, int SortOrder)
{
    public static StudentQuestionResponse FromQuestion(Question question) => new(
        question.Id, question.SessionId, question.Text, question.Type,
        question.Type == QuestionType.Matching
            ? question.MatchingPairs.Select(pair => pair.Match).Order(StringComparer.Ordinal).ToList()
            : question.Options,
        question.MatchingPairs.Select(pair => new MatchingPair { Prompt = pair.Prompt, Match = string.Empty }).ToList(),
        question.SortOrder);
}
