using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.WebApi;

namespace Pulse.Tests;

public class QuestionCreateEndpointTests
{
    [Fact]
    public void PostCreateReturnsOkAndNormalizesOptionsForValidMcQuestion()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var question = new Question
        {
            Text = "Pick one",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { " Option A ", "Option B", "" }
        };

        var result = QuestionEndpointHandlers.CreateQuestion(question, repo, new QuestionService());

        var ok = Assert.IsType<Ok<Question>>(result.Result);
        Assert.Equal(new[] { "Option A", "Option B" }, ok.Value!.Options);

        var persisted = repo.GetById(ok.Value.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new[] { "Option A", "Option B" }, persisted.Options);
    }

    [Fact]
    public void PostCreateReturnsBadRequestWhenMcQuestionHasFewerThanTwoOptions()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var question = new Question
        {
            Text = "Pick one",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Only one" }
        };

        var result = QuestionEndpointHandlers.CreateQuestion(question, repo, new QuestionService());

        var badRequest = Assert.IsType<BadRequest<string>>(result.Result);
        Assert.Equal("Multiple-choice questions must include at least 2 options.", badRequest.Value);
    }

    [Fact]
    public void PostCreateReturnsOkForNonMcQuestionWithNoOptions()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var question = new Question
        {
            Text = "How do you feel?",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };

        var result = QuestionEndpointHandlers.CreateQuestion(question, repo, new QuestionService());

        Assert.IsType<Ok<Question>>(result.Result);
    }
}
