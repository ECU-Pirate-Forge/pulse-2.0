using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class ResponseAggregationServiceTests
{
    private Mock<IResponseRepository> BuildRepo(List<Response> responses)
    {
        var repo = new Mock<IResponseRepository>();
        repo.Setup(r => r.GetByQuestionId(It.IsAny<Guid>()))
            .Returns((Guid id) => responses.Where(r => r.QuestionId == id));
        return repo;
    }

    [Fact]
    public void GetResponseCounts_ValidQuestion_ReturnsCorrectCounts()
    {
        var questionId = Guid.NewGuid();
        var responses = new List<Response>
        {
            new() { QuestionId = questionId, DeviceId = "d1", Value = "A" },
            new() { QuestionId = questionId, DeviceId = "d2", Value = "A" },
            new() { QuestionId = questionId, DeviceId = "d3", Value = "B" },
        };
        var svc = new ResponseAggregationService(BuildRepo(responses).Object);

        var result = svc.GetResponseCounts(questionId);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Tallies.First(t => t.Option == "A").Count);
        Assert.Equal(1, result.Tallies.First(t => t.Option == "B").Count);
    }

    [Fact]
    public void GetResponseCounts_NoResponses_ReturnsEmptyTallies()
    {
        var questionId = Guid.NewGuid();
        var svc = new ResponseAggregationService(BuildRepo([]).Object);

        var result = svc.GetResponseCounts(questionId);

        Assert.True(result.IsValid);
        Assert.Empty(result.Tallies);
    }

    [Fact]
    public void GetResponseCounts_InvalidQuestionId_ReturnsFailure()
    {
        var svc = new ResponseAggregationService(BuildRepo([]).Object);

        var result = svc.GetResponseCounts(Guid.Empty);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GetResponseCounts_QuestionIncludesUnselectedOptionsInAuthoredOrder()
    {
        var question = new Question { Id = Guid.NewGuid(), Text = "Choose one", Type = QuestionType.MultipleChoice, Options = ["C", "A", "B"] };
        var service = new ResponseAggregationService(BuildRepo(
            [new Response { QuestionId = question.Id, DeviceId = "d1", Value = "A" }]).Object);

        var result = service.GetResponseCounts(question);

        Assert.Equal(
            [new QuestionTally("C", 0), new QuestionTally("A", 1), new QuestionTally("B", 0)],
            result.Tallies);
    }

    [Theory]
    [InlineData(QuestionType.MultipleChoice)]
    [InlineData(QuestionType.MultipleAnswer)]
    [InlineData(QuestionType.TrueFalse)]
    public void GetResponseCounts_QuestionWithoutResponsesIncludesZeroTallies(QuestionType type)
    {
        var question = new Question { Id = Guid.NewGuid(), Text = "Choose", Type = type, Options = type == QuestionType.TrueFalse ? [] : ["A", "B"] };
        var service = new ResponseAggregationService(BuildRepo([]).Object);

        var result = service.GetResponseCounts(question);

        Assert.Equal(type == QuestionType.TrueFalse ? ["True", "False"] : new[] { "A", "B" },
            result.Tallies.Select(tally => tally.Option));
        Assert.All(result.Tallies, tally => Assert.Equal(0, tally.Count));
    }

    [Fact]
    public void GetResponseCounts_MultipleAnswersCountsStudentsRatherThanSelections()
    {
        var question = new Question { Id = Guid.NewGuid(), Text = "Choose all", Type = QuestionType.MultipleAnswer, Options = ["A", "B", "C"] };
        var service = new ResponseAggregationService(BuildRepo([
            new Response { QuestionId = question.Id, DeviceId = "d1", Value = "[\"A\",\"A\",\"B\"]" },
            new Response { QuestionId = question.Id, DeviceId = "d2", Value = "[\"B\"]" }
        ]).Object);

        var result = service.GetResponseCounts(question);

        Assert.Equal(
            [new QuestionTally("A", 1), new QuestionTally("B", 2), new QuestionTally("C", 0)],
            result.Tallies);
    }

    [Fact]
    public void GetResponseCounts_MatchingIncludesAllPromptChoiceCombinations()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(), Text = "Match", Type = QuestionType.Matching,
            MatchingPairs = [new() { Prompt = "One", Match = "1" }, new() { Prompt = "Two", Match = "2" }]
        };
        var service = new ResponseAggregationService(BuildRepo([
            new Response { QuestionId = question.Id, DeviceId = "d1", Value = "[\"2\",\"1\"]" }
        ]).Object);

        var result = service.GetResponseCounts(question);

        Assert.Equal([
            new QuestionTally("One: 1", 0), new QuestionTally("One: 2", 1),
            new QuestionTally("Two: 1", 1), new QuestionTally("Two: 2", 0)
        ], result.Tallies);
    }
}
