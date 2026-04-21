using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi;

public static class SessionResultsEndpointHandlers
{
    public static async Task<IResult> GetSessionResults(
        Guid id,
        HttpContext context,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        IResponseRepository responseRepo)
    {
        var instructorCode = context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();

        if (string.IsNullOrWhiteSpace(instructorCode))
            return Results.Unauthorized();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(403);

        var questions = questionRepo.GetBySessionId(id).OrderBy(q => q.SortOrder).ToList();

        var aggregationService = new ResponseAggregationService(responseRepo);

        var questionResults = questions.Select(q =>
        {
            var aggregation = aggregationService.GetResponseCounts(q.Id);
            return new QuestionResult(
                q.Id,
                q.Text,
                q.Type.ToString(),
                aggregation.Tallies.Select(t => new TallyResult(t.Option, t.Count)).ToList(),
                aggregation.Tallies.Sum(t => t.Count));
        }).ToList();

        var totalResponses = questionResults.Sum(q => q.TotalResponses);

        return Results.Ok(new SessionResultsResponse(id, session.Title, questionResults, totalResponses));
    }
}

public record TallyResult(string Option, int Count);
public record QuestionResult(Guid QuestionId, string Text, string Type, List<TallyResult> Tallies, int TotalResponses);
public record SessionResultsResponse(Guid SessionId, string Title, List<QuestionResult> Questions, int TotalResponses);
