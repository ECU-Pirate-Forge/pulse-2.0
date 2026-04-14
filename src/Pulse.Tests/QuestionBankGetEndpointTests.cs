using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class QuestionBankGetEndpointTests
{
    private List<QuestionBankItem> _items =
    [
        new QuestionBankItem { Text = "What is photosynthesis?", Type = QuestionType.MultipleChoice, Options = ["A", "B"] },
        new QuestionBankItem { Text = "What is gravity?", Type = QuestionType.MultipleChoice, Options = ["A", "B"] },
        new QuestionBankItem { Text = "Rate your experience", Type = QuestionType.LikertScale },
    ];

    private Mock<IQuestionBankRepository> BuildRepo(List<QuestionBankItem>? items = null)
    {
        var repo = new Mock<IQuestionBankRepository>();
        repo.Setup(r => r.Search(It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns((string? text, int? type) =>
            {
                var all = items ?? _items;
                if (!string.IsNullOrWhiteSpace(text))
                    all = all.Where(x => x.Text.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
                if (type is not null)
                    all = all.Where(x => (int)x.Type == type.Value).ToList();
                return all;
            });
        return repo;
    }

    [Fact]
    public void GetQuestionBankItems_NoFilters_ReturnsAll()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object);

        var ok = Assert.IsType<Ok<QuestionBankListResponse>>(result.Result);
        Assert.Equal(3, ok.Value!.TotalCount);
        Assert.Equal(3, ok.Value.Items.Count);
    }

    [Fact]
    public void GetQuestionBankItems_FilterByText_ReturnsMatching()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object, text: "What is");

        var ok = Assert.IsType<Ok<QuestionBankListResponse>>(result.Result);
        Assert.Equal(2, ok.Value!.TotalCount);
    }

    [Fact]
    public void GetQuestionBankItems_FilterByType_ReturnsMatching()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object, type: (int)QuestionType.LikertScale);

        var ok = Assert.IsType<Ok<QuestionBankListResponse>>(result.Result);
        Assert.Equal(1, ok.Value!.TotalCount);
    }

    [Fact]
    public void GetQuestionBankItems_Pagination_ReturnsCorrectPage()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object, page: 1, pageSize: 2);

        var ok = Assert.IsType<Ok<QuestionBankListResponse>>(result.Result);
        Assert.Equal(3, ok.Value!.TotalCount);
        Assert.Equal(2, ok.Value.Items.Count);
    }

    [Fact]
    public void GetQuestionBankItems_EmptyResult_ReturnsZero()
    {
        var repo = BuildRepo([]);

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object);

        var ok = Assert.IsType<Ok<QuestionBankListResponse>>(result.Result);
        Assert.Equal(0, ok.Value!.TotalCount);
        Assert.Empty(ok.Value.Items);
    }

    [Fact]
    public void GetQuestionBankItems_InvalidPage_Returns400()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object, page: 0);

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public void GetQuestionBankItems_InvalidPageSize_Returns400()
    {
        var repo = BuildRepo();

        var result = QuestionBankEndpointHandlers.GetQuestionBankItems(repo.Object, pageSize: 0);

        Assert.IsType<BadRequest<string>>(result.Result);
    }
}
