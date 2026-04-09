using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class QuestionBankEndpointTests
{
    private Mock<IQuestionBankRepository> BuildRepo()
    {
        var repo = new Mock<IQuestionBankRepository>();
        repo.Setup(r => r.Insert(It.IsAny<QuestionBankItem>()))
            .Returns((QuestionBankItem item) => item);
        return repo;
    }

    [Fact]
    public void CreateQuestionBankItem_ValidRequest_Returns201()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "What is the capital of France?",
            Type = QuestionType.MultipleChoice,
            Options = ["Paris", "London"]
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object);

        var created = Assert.IsType<Created<QuestionBankItem>>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(created.Value);
        Assert.Equal("What is the capital of France?", created.Value.Text);
    }

    [Fact]
    public void CreateQuestionBankItem_EmptyText_Returns400()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object);

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public void CreateQuestionBankItem_NullType_Returns400()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "Some question",
            Type = null,
            Options = ["A", "B"]
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object);

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public void CreateQuestionBankItem_MultipleChoiceWithOneOption_Returns400()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "Some question",
            Type = QuestionType.MultipleChoice,
            Options = ["Only one"]
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object);

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public void CreateQuestionBankItem_NonMultipleChoice_NoOptions_Returns201()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "Rate your experience",
            Type = QuestionType.LikertScale,
            Options = []
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object);

        Assert.IsType<Created<QuestionBankItem>>(result.Result);
    }
}
