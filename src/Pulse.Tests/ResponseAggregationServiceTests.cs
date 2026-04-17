using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
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
}
