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
    public void ValidateQuestion_MultipleAnswerWithOneOption_ReturnsFailure()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.MultipleAnswer,
            Options = ["Only one"]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.False(result.IsValid);
        Assert.Equal("Multiple-answer questions must include at least 2 options.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_TrueFalseWithNoOptions_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.TrueFalse,
            Options = []
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_MatchingWithTwoCompletePairs_ReturnsSuccess()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = "HTTP", Match = "Web protocol" },
                new MatchingPair { Prompt = "HTML", Match = "Markup language" }
            ]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateQuestion_MatchingWithIncompletePair_ReturnsFailure()
    {
        var dto = new QuestionDTO
        {
            Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = "HTTP", Match = "" },
                new MatchingPair { Prompt = "HTML", Match = "Markup language" }
            ]
        };

        var result = _svc.ValidateQuestion(dto);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Matching questions must include at least 2 complete prompt-and-match pairs.",
            result.ErrorMessage);
    }

    [Theory]
    [InlineData(QuestionType.LegacyLikertScale)]
    [InlineData(QuestionType.LegacyOpenEnded)]
    public void ValidateQuestion_LegacyType_ReturnsFailure(QuestionType type)
    {
        var result = _svc.ValidateQuestion(new QuestionDTO { Type = type });

        Assert.False(result.IsValid);
        Assert.Equal("This legacy question type can no longer be authored.", result.ErrorMessage);
    }
}
