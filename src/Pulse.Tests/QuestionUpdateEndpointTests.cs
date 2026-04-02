using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.WebApi;

namespace Pulse.Tests;

public class QuestionUpdateEndpointTests
{
    [Fact]
    public void PutUpdateReturnsOkAndPersistsUpdatedQuestionForValidPayload()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var original = new Question
        {
            Id = Guid.NewGuid(),
            Text = "Original text",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };

        repo.Insert(original);

        var request = new UpdateQuestionRequest
        {
            Text = "Updated text",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { " Option A ", "Option B" }
        };

        var result = QuestionEndpointHandlers.UpdateQuestion(original.Id, request, repo);

        var ok = Assert.IsType<Ok<Question>>(result.Result);
        Assert.Equal(original.Id, ok.Value!.Id);
        Assert.Equal("Updated text", ok.Value.Text);
        Assert.Equal(QuestionType.MultipleChoice, ok.Value.Type);
        Assert.Equal(new[] { "Option A", "Option B" }, ok.Value.Options);

        var persisted = repo.GetById(original.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated text", persisted.Text);
        Assert.Equal(QuestionType.MultipleChoice, persisted.Type);
        Assert.Equal(new[] { "Option A", "Option B" }, persisted.Options);
    }

    [Fact]
    public void PutUpdateReturnsBadRequestWhenMultipleChoiceHasLessThanTwoOptions()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var existing = new Question
        {
            Id = Guid.NewGuid(),
            Text = "Original",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };

        repo.Insert(existing);

        var request = new UpdateQuestionRequest
        {
            Text = "Updated",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Only one" }
        };

        var result = QuestionEndpointHandlers.UpdateQuestion(existing.Id, request, repo);

        var badRequest = Assert.IsType<BadRequest<string>>(result.Result);
        Assert.Equal("Multiple-choice questions must include at least 2 options.", badRequest.Value);
    }

    [Fact]
    public void PutUpdateReturnsNotFoundWhenQuestionDoesNotExist()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var request = new UpdateQuestionRequest
        {
            Text = "Updated",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };

        var result = QuestionEndpointHandlers.UpdateQuestion(Guid.NewGuid(), request, repo);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public void PutUpdateReturnsBadRequestWhenIdIsEmpty()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var request = new UpdateQuestionRequest
        {
            Text = "Updated",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };

        var result = QuestionEndpointHandlers.UpdateQuestion(Guid.Empty, request, repo);

        var badRequest = Assert.IsType<BadRequest<string>>(result.Result);
        Assert.Equal("Question id is invalid.", badRequest.Value);
    }

    [Fact]
    public void DeleteReturnsNoContentAndRemovesExistingQuestion()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = "To be deleted",
            Type = QuestionType.OpenEnded,
            Options = new List<string>()
        };
        repo.Insert(question);

        var result = QuestionEndpointHandlers.DeleteQuestion(question.Id, repo);

        Assert.IsType<NoContent>(result.Result);
        Assert.Null(repo.GetById(question.Id));
    }

    [Fact]
    public void DeleteReturnsNotFoundForUnknownId()
    {
        using var db = new LiteDB.LiteDatabase("Filename=:memory:");
        var repo = new QuestionRepository(db);

        var result = QuestionEndpointHandlers.DeleteQuestion(Guid.NewGuid(), repo);

        Assert.IsType<NotFound>(result.Result);
    }
}
