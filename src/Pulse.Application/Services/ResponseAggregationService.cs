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

        var responses = _repo.GetByQuestionId(question.Id).ToList();
        var values = responses
            .SelectMany(response => DecodeValues(response.Value)
                .Select(value => (Value: value, response.DeviceId)))
            .ToList();

        if (question.Type == QuestionType.Matching)
        {
            values = responses.SelectMany(response =>
            {
                var matches = DecodeValues(response.Value);
                return question.MatchingPairs.Select((pair, index) =>
                    (Value: $"{pair.Prompt}: {matches.ElementAtOrDefault(index) ?? string.Empty}", response.DeviceId));
            }).Where(item => !item.Value.EndsWith(": ", StringComparison.Ordinal)).ToList();
        }

        var counts = values
            .GroupBy(item => item.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.DeviceId).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);
        IEnumerable<string> options = question.Type switch
        {
            QuestionType.TrueFalse => ["True", "False"],
            QuestionType.Matching => question.MatchingPairs.SelectMany(pair =>
                question.MatchingPairs.Select(choice => $"{pair.Prompt}: {choice.Match}")),
            _ => question.Options
        };

        return AggregationResult.Success(options.Concat(counts.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(option => new QuestionTally(option, counts.GetValueOrDefault(option)))
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
