using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class ModeratedQuestionBankTests
{
    [Fact]
    public void SubmitSuggestion_CreatesAnonymousPendingStudentItem()
    {
        var repository = Repository();

        var result = QuestionBankEndpointHandlers.SubmitQuestionSuggestion(
            new SubmitQuestionSuggestionRequest
            {
                Text = "Which protocol secures HTTP?",
                Type = QuestionType.MultipleChoice,
                Options = ["HTTPS", "FTP"],
                Course = "CSCI 3000",
                Topic = "Networking",
                Tags = ["security", "web"]
            },
            repository.Object,
            new QuestionService());

        var accepted = Assert.IsType<Accepted<QuestionBankItem>>(result.Result);
        Assert.Equal(QuestionModerationStatus.Pending, accepted.Value!.ModerationStatus);
        Assert.Equal(QuestionSource.StudentSuggestion, accepted.Value.Source);
        Assert.Equal("Anonymous student", accepted.Value.ContributorAttribution);
        repository.Verify(repo => repo.Insert(It.Is<QuestionBankItem>(item =>
            item.ModerationStatus == QuestionModerationStatus.Pending)), Times.Once);
    }

    [Theory]
    [InlineData(QuestionModerationStatus.Approved)]
    [InlineData(QuestionModerationStatus.Rejected)]
    public void ModerateQuestion_UpdatesStatusAndTimestamp(QuestionModerationStatus status)
    {
        var item = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "Pending question",
            Type = QuestionType.TrueFalse,
            ModerationStatus = QuestionModerationStatus.Pending
        };
        var repository = Repository(item);

        var result = QuestionBankEndpointHandlers.ModerateQuestionBankItem(
            item.Id,
            new ModerateQuestionBankItemRequest { ModerationStatus = status },
            repository.Object,
            new QuestionService());

        Assert.IsType<Ok<QuestionBankItem>>(result.Result);
        Assert.Equal(status, item.ModerationStatus);
        Assert.True(item.UpdatedAt >= item.CreatedAt);
        repository.Verify(repo => repo.Update(item), Times.Once);
    }

    [Fact]
    public async Task ImportQuestions_RejectsPendingBankItem()
    {
        var sessionId = Guid.NewGuid();
        var pending = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "Not approved",
            Type = QuestionType.TrueFalse,
            ModerationStatus = QuestionModerationStatus.Pending
        };
        var bank = Repository(pending);
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(repo => repo.GetByIdAsync(sessionId)).ReturnsAsync(new Session
        {
            Id = sessionId,
            InstructorCode = "INST",
            Status = SessionStatus.Draft
        });
        using var database = new LiteDB.LiteDatabase("Filename=:memory:");
        var context = new DefaultHttpContext();
        context.Items["InstructorCode"] = "INST";

        var result = await QuestionBankImportEndpointHandlers.ImportQuestions(
            sessionId,
            new ImportQuestionsRequest { QuestionBankItemIds = [pending.Id] },
            context,
            sessions.Object,
            bank.Object,
            new QuestionRepository(database),
            new QuestionService());

        var badRequest = Assert.IsType<BadRequest<string>>(result);
        Assert.Contains("approved", badRequest.Value!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositorySearch_FiltersTaxonomyModerationAndSortsNewest()
    {
        using var database = new LiteDB.LiteDatabase("Filename=:memory:");
        var repository = new QuestionBankRepository(database);
        repository.Insert(new QuestionBankItem
        {
            Text = "Older networking question",
            Type = QuestionType.TrueFalse,
            Course = "CSCI 3000",
            Topic = "Networks",
            Tags = ["protocols"],
            ModerationStatus = QuestionModerationStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        });
        repository.Insert(new QuestionBankItem
        {
            Text = "Newest networking question",
            Type = QuestionType.TrueFalse,
            Course = "CSCI 3000",
            Topic = "Networks",
            Tags = ["protocols", "security"],
            ModerationStatus = QuestionModerationStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });
        repository.Insert(new QuestionBankItem
        {
            Text = "Pending networking question",
            Type = QuestionType.TrueFalse,
            Course = "CSCI 3000",
            Topic = "Networks",
            Tags = ["security"],
            ModerationStatus = QuestionModerationStatus.Pending
        });

        var results = repository.Search(new QuestionBankQuery(
            Course: "CSCI 3000",
            Topic: "Networks",
            Tag: "security",
            ModerationStatus: QuestionModerationStatus.Approved,
            Sort: QuestionBankSort.Newest)).ToList();

        var result = Assert.Single(results);
        Assert.Equal("Newest networking question", result.Text);
    }

    private static Mock<IQuestionBankRepository> Repository(QuestionBankItem? existing = null)
    {
        var repository = new Mock<IQuestionBankRepository>();
        repository.Setup(repo => repo.Insert(It.IsAny<QuestionBankItem>()))
            .Returns((QuestionBankItem item) => item);
        if (existing is not null)
            repository.Setup(repo => repo.GetById(existing.Id)).Returns(existing);
        repository.Setup(repo => repo.Update(It.IsAny<QuestionBankItem>())).Returns(true);
        return repository;
    }
}
