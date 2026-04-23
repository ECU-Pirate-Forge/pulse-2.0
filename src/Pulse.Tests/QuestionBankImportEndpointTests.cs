using LiteDB;
using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class QuestionBankImportEndpointTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _bankItemId = Guid.NewGuid();

    private Mock<ISessionRepository> BuildSessionRepo(Session? session = null)
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(session);
        return repo;
    }

    private Mock<IQuestionBankRepository> BuildBankRepo(QuestionBankItem? item = null)
    {
        var repo = new Mock<IQuestionBankRepository>();
        repo.Setup(r => r.GetById(It.IsAny<Guid>()))
            .Returns(item);
        return repo;
    }

    private QuestionRepository BuildQuestionRepo()
    {
        var db = new LiteDatabase("Filename=:memory:");
        return new QuestionRepository(db);
    }

    private HttpContext BuildContext(string? instructorCode = null)
    {
        var context = new DefaultHttpContext();
        if (instructorCode is not null)
            context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }

    [Fact]
    public async Task ImportQuestions_ValidRequest_Returns200WithCreatedQuestions()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST001" };
        var bankItem = new QuestionBankItem
        {
            Id = _bankItemId,
            Text = "What is 2+2?",
            Type = QuestionType.MultipleChoice,
            Options = ["3", "4", "5", "6"]
        };

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId] },
            BuildContext("INST001"),
            BuildSessionRepo(session).Object,
            BuildBankRepo(bankItem).Object,
            BuildQuestionRepo());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<Question>>>(result);
        Assert.NotNull(ok.Value);
        Assert.Single(ok.Value);
        Assert.Equal("What is 2+2?", ok.Value[0].Text);
        Assert.Equal(_sessionId, ok.Value[0].SessionId);
        Assert.NotEqual(_bankItemId, ok.Value[0].Id);
    }

    [Fact]
    public async Task ImportQuestions_EmptyList_Returns400()
    {
        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [] },
            BuildContext("INST001"),
            BuildSessionRepo().Object,
            BuildBankRepo().Object,
            BuildQuestionRepo());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task ImportQuestions_UnknownSession_Returns404()
    {
        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId] },
            BuildContext("INST001"),
            BuildSessionRepo(null).Object,
            BuildBankRepo().Object,
            BuildQuestionRepo());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound<string>>(result);
    }

    [Fact]
    public async Task ImportQuestions_MissingInstructorCode_Returns401()
    {
        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId] },
            BuildContext(null),
            BuildSessionRepo().Object,
            BuildBankRepo().Object,
            BuildQuestionRepo());

        var statusResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(401, statusResult.StatusCode);
    }

    [Fact]
    public async Task ImportQuestions_WrongInstructorCode_Returns403()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "RIGHTCODE" };

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId] },
            BuildContext("WRONGCODE"),
            BuildSessionRepo(session).Object,
            BuildBankRepo().Object,
            BuildQuestionRepo());

        var statusResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task ImportQuestions_InvalidBankItemId_Returns400()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST001" };

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [Guid.NewGuid()] },
            BuildContext("INST001"),
            BuildSessionRepo(session).Object,
            BuildBankRepo(null).Object,
            BuildQuestionRepo());

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task ImportQuestions_CreatedQuestionsAreIndependentFromBank()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST001" };
        var bankItem = new QuestionBankItem
        {
            Id = _bankItemId,
            Text = "Original text",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        };

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId] },
            BuildContext("INST001"),
            BuildSessionRepo(session).Object,
            BuildBankRepo(bankItem).Object,
            BuildQuestionRepo());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<Question>>>(result);
        Assert.NotEqual(_bankItemId, ok.Value![0].Id);
        Assert.Equal(2, ok.Value[0].Options.Count);
        // Mutate the bank item and verify the imported question is unaffected
        bankItem.Options.Add("C");
        Assert.Equal(2, ok.Value[0].Options.Count);
    }

    [Fact]
    public async Task ImportQuestions_MultipleItems_SetsUniqueSortOrders()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST001" };
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var item1 = new QuestionBankItem { Id = id1, Text = "Q1", Type = QuestionType.MultipleChoice, Options = ["A", "B"] };
        var item2 = new QuestionBankItem { Id = id2, Text = "Q2", Type = QuestionType.LikertScale, Options = [] };

        var repo = new Mock<IQuestionBankRepository>();
        repo.Setup(r => r.GetById(id1)).Returns(item1);
        repo.Setup(r => r.GetById(id2)).Returns(item2);

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [id1, id2] },
            BuildContext("INST001"),
            BuildSessionRepo(session).Object,
            repo.Object,
            BuildQuestionRepo());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<Question>>>(result);
        Assert.Equal(2, ok.Value!.Count);
        Assert.NotEqual(ok.Value[0].Id, ok.Value[1].Id);
        Assert.Equal(0, ok.Value[0].SortOrder);
        Assert.Equal(1, ok.Value[1].SortOrder);
    }

    [Fact]
    public async Task ImportQuestions_DuplicateIds_DeduplicatesImport()
    {
        var session = new Session { Id = _sessionId, InstructorCode = "INST001" };
        var bankItem = new QuestionBankItem { Id = _bankItemId, Text = "Q1", Type = QuestionType.MultipleChoice, Options = ["A", "B"] };

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            _sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [_bankItemId, _bankItemId] },
            BuildContext("INST001"),
            BuildSessionRepo(session).Object,
            BuildBankRepo(bankItem).Object,
            BuildQuestionRepo());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<List<Question>>>(result);
        Assert.Single(ok.Value!);
    }
}
