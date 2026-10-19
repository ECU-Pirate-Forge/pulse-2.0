using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Pulse.Application.Services;
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

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

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

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

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

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

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

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public void CreateQuestionBankItem_TrueFalse_NoOptions_Returns201()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "The statement is true.",
            Type = QuestionType.TrueFalse,
            Options = []
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

        Assert.IsType<Created<QuestionBankItem>>(result.Result);
    }

    [Fact]
    public void CreateQuestionBankItem_Matching_PersistsNormalizedPairs()
    {
        var repo = BuildRepo();
        var request = new CreateQuestionBankItemRequest
        {
            Text = "Match each term",
            Type = QuestionType.Matching,
            MatchingPairs =
            [
                new MatchingPair { Prompt = " HTTP ", Match = " Web protocol " },
                new MatchingPair { Prompt = " HTML ", Match = " Markup language " }
            ]
        };

        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(request, repo.Object, new QuestionService());

        var created = Assert.IsType<Created<QuestionBankItem>>(result.Result);
        Assert.Equal("HTTP", created.Value!.MatchingPairs[0].Prompt);
        Assert.Equal("Markup language", created.Value.MatchingPairs[1].Match);
    }

    [Fact]
    public void CreateQuestionBankItem_LegacyType_Returns400()
    {
        var result = QuestionBankEndpointHandlers.CreateQuestionBankItem(
            new CreateQuestionBankItemRequest
            {
                Text = "Legacy",
                Type = QuestionType.LegacyOpenEnded
            },
            BuildRepo().Object,
            new QuestionService());

        Assert.IsType<BadRequest<string>>(result.Result);
    }
}
