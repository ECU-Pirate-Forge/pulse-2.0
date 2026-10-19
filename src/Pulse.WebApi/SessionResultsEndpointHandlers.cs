using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
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
        {
            return Results.Json(
                new { error = "InstructorCode is required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (!string.Equals(session.InstructorCode, instructorCode, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return TypedResults.Ok(BuildInstructorResults(session, questionRepo, responseRepo));
    }

    public static async Task<IResult> GetStudentResults(
        Guid id,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        IResponseRepository responseRepo)
    {
        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null)
            return Results.NotFound();

        if (session.ResultsVisibility != ResultsVisibility.Revealed)
            return TypedResults.Ok(new StudentResultsStatusResponse(ResultsVisibility.Blinded, session.ResetVersion, session.Status));

        var instructorResults = BuildInstructorResults(session, questionRepo, responseRepo);
        return TypedResults.Ok(new StudentSurveyResultsResponse(
            instructorResults.SessionId,
            instructorResults.Title,
            instructorResults.Questions,
            session.ResetVersion));
    }

    private static InstructorSurveyResultsResponse BuildInstructorResults(
        Session session,
        QuestionRepository questionRepo,
        IResponseRepository responseRepo)
    {
        var questions = questionRepo.GetBySessionId(session.Id)
            .OrderBy(question => question.SortOrder)
            .ToList();
        var aggregationService = new ResponseAggregationService(responseRepo);
        var questionResults = questions.Select(question =>
        {
            var aggregation = aggregationService.GetResponseCounts(question);
            var responseCount = responseRepo.GetByQuestionId(question.Id)
                .Select(response => response.DeviceId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            return new SurveyQuestionResultsResponse(
                question.Id,
                question.Text,
                question.Type.ToString(),
                aggregation.Tallies
                    .Select(tally => new OptionTallyResponse(tally.Option, tally.Count))
                    .ToList(),
                responseCount,
                question.Type == QuestionType.Matching
                    ? question.MatchingPairs.Select(pair => $"{pair.Prompt}: {pair.Match}").ToList()
                    : question.CorrectAnswers.ToList());
        }).ToList();

        var participantCount = responseRepo.GetBySession(session.Id)
            .Select(response => response.DeviceId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var completedCount = responseRepo.GetSubmissionsBySession(session.Id)
            .Select(submission => submission.DeviceId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new InstructorSurveyResultsResponse(
            session.Id,
            session.Title,
            participantCount,
            completedCount,
            questionResults);
    }
}
