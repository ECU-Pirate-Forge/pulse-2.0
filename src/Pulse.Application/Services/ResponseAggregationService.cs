using Pulse.Common.Services;

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
}
