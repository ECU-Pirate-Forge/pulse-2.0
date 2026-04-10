using Pulse.Application.Services;
using Pulse.Domain.Entities;

namespace Pulse.Tests;

public class QuestionServiceTests
{
    private readonly QuestionService _svc = new();

    [Fact]
    public void ValidateQuestion_McWithZeroOptions_ReturnsFailure()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleChoice,
            Options = []
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.False(result.IsValid);
        Assert.Equal("Multiple-choice questions must include at least 2 options.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_McWithOneOption_ReturnsFailure()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleChoice,
            Options = ["Only one"]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.False(result.IsValid);
        Assert.Equal("Multiple-choice questions must include at least 2 options.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_McWithOneRealOptionAndBlanks_ReturnsFailure()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleChoice,
            Options = ["Real option", "   ", ""]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.False(result.IsValid);
        Assert.Equal("Multiple-choice questions must include at least 2 options.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_McWithTwoOptions_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleChoice,
            Options = ["Option A", "Option B"]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_McWithThreeOptions_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleChoice,
            Options = ["Option A", "Option B", "Option C"]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_OpenEndedWithNoOptions_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.OpenEnded,
            Options = []
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_LikertScaleWithNoOptions_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.LikertScale,
            Options = []
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_LikertScaleWithOneOption_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.LikertScale,
            Options = ["Strongly Agree"]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }
}
