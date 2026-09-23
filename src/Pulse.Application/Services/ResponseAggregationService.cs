using Pulse.Common.Services;
using Pulse.Domain.Entities;
using System.Text.Json;

namespace Pulse.Application.Services;

public sealed record QuestionTally(string Option, int Count);

public sealed record AggregationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<QuestionTally> Tallies { get; init; } = [];

    public static AggregationResult Success(IReadOnlyList<QuestionTally> tallies) =>
        new() { IsValid = true, Tallies = tallies };

    public static AggregationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

public class ResponseAggregationService
{
    private readonly IResponseRepository _repo;

    public ResponseAggregationService(IResponseRepository repo)
    {
        _repo = repo;
    }

    public AggregationResult GetResponseCounts(Guid questionId)
    {
        if (questionId == Guid.Empty)
            return AggregationResult.Failure("Question ID is invalid.");

        var responses = _repo.GetByQuestionId(questionId).ToList();

        var tallies = responses
            .GroupBy(r => r.Value)
            .Select(g => new QuestionTally(g.Key, g.Count()))
            .ToList();

        return AggregationResult.Success(tallies);
    }

    public AggregationResult GetResponseCounts(Question question)
    {
        if (question.Id == Guid.Empty)
            return AggregationResult.Failure("Question ID is invalid.");

        var values = _repo.GetByQuestionId(question.Id)
            .SelectMany(response => DecodeValues(response.Value))
            .ToList();

        if (question.Type == QuestionType.Matching)
        {
            var responses = _repo.GetByQuestionId(question.Id).ToList();
            values = responses.SelectMany(response =>
            {
                var matches = DecodeValues(response.Value);
                return question.MatchingPairs.Select((pair, index) =>
                    $"{pair.Prompt}: {matches.ElementAtOrDefault(index) ?? string.Empty}");
            }).Where(value => !value.EndsWith(": ", StringComparison.Ordinal)).ToList();
        }

        return AggregationResult.Success(values
            .GroupBy(value => value)
            .Select(group => new QuestionTally(group.Key, group.Count()))
            .ToList());
    }

    private static IReadOnlyList<string> DecodeValues(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [value];
        }
    }
}
