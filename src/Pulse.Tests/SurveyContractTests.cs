using System.Text.Json;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class SurveyContractTests
{
    [Fact]
    public void SubmitSurveyRequest_RepresentsMultipleSelectionsAsOneAnswer()
    {
        var questionId = Guid.NewGuid();
        var request = new SubmitSurveyRequest(
            "7ed9232b-e5ac-4588-8690-8cc50e4c650c",
            [new SurveyAnswerRequest(questionId, ["Option A", "Option C"])]);

        Assert.Single(request.Answers);
        Assert.Equal(questionId, request.Answers[0].QuestionId);
        Assert.Equal(["Option A", "Option C"], request.Answers[0].Values);
    }

    [Fact]
    public void SessionSummary_SerializesTypedStateAsReadableStrings()
    {
        var summary = new SessionSummaryResponse(
            Guid.NewGuid(),
            "Week 1 feedback",
            SessionMode.Survey,
            SessionStatus.Active,
            ResultsVisibility.Blinded);

        var json = JsonSerializer.Serialize(summary);

        Assert.Contains("\"mode\":\"Survey\"", json);
        Assert.Contains("\"status\":\"Active\"", json);
        Assert.Contains("\"resultsVisibility\":\"Blinded\"", json);
    }

    [Fact]
    public void StudentResultsStatus_DoesNotContainAggregateResults()
    {
        var status = new StudentResultsStatusResponse(ResultsVisibility.Blinded);

        var json = JsonSerializer.Serialize(status);

        Assert.Equal("{\"resultsVisibility\":\"Blinded\"}", json);
    }
}
