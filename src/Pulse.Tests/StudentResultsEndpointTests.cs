using System.Text.Json;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class StudentResultsEndpointTests
{
    [Fact]
    public async Task GetStudentResults_WhileBlinded_ReturnsVisibilityWithoutTallies()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var session = CreateSession(ResultsVisibility.Blinded);

        var result = await SessionResultsEndpointHandlers.GetStudentResults(
            session.Id,
            CreateSessionRepository(session).Object,
            new QuestionRepository(database),
            new ResponseRepository(database));

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<StudentResultsStatusResponse>>(result);
        Assert.Equal(ResultsVisibility.Blinded, ok.Value!.ResultsVisibility);
        Assert.DoesNotContain("tallies", System.Text.Json.JsonSerializer.Serialize(ok.Value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStudentResults_AfterReveal_ReturnsAggregateTallies()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var session = CreateSession(ResultsVisibility.Revealed);
        var questionRepo = new QuestionRepository(database);
        var responseRepo = new ResponseRepository(database);
        var question = questionRepo.Insert(new Question
        {
            SessionId = session.Id,
            Text = "Choose all",
            Type = QuestionType.MultipleAnswer,
            Options = ["A", "B", "C"]
        });
        responseRepo.Upsert(new Response
        {
            SessionId = session.Id,
            QuestionId = question.Id,
            DeviceId = Guid.NewGuid().ToString(),
            Value = "[\"A\",\"C\"]"
        });

        var result = await SessionResultsEndpointHandlers.GetStudentResults(
            session.Id,
            CreateSessionRepository(session).Object,
            questionRepo,
            responseRepo);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<StudentSurveyResultsResponse>>(result);
        Assert.Equal(1, ok.Value!.Questions.Single().Tallies.Single(tally => tally.Value == "A").Count);
        Assert.Equal(1, ok.Value.Questions.Single().Tallies.Single(tally => tally.Value == "C").Count);
    }

    [Fact]
    public async Task GetInstructorResults_UsesUniqueParticipantAndCompletionCounts()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var session = CreateSession(ResultsVisibility.Blinded);
        session.InstructorCode = "INST";
        var questionRepo = new QuestionRepository(database);
        var responseRepo = new ResponseRepository(database);
        var question = questionRepo.Insert(new Question
        {
            SessionId = session.Id,
            Text = "Choose one",
            Type = QuestionType.MultipleChoice,
            Options = ["A", "B"]
        });
        responseRepo.SubmitSurvey(
            [
                new Response
                {
                    SessionId = session.Id,
                    QuestionId = question.Id,
                    DeviceId = "device-1",
                    Value = "[\"A\"]"
                }
            ],
            new SurveySubmission
            {
                SessionId = session.Id,
                DeviceId = "device-1",
                SubmittedAt = DateTime.UtcNow
            });
        responseRepo.Upsert(new Response
        {
            SessionId = session.Id,
            QuestionId = question.Id,
            DeviceId = "device-2",
            Value = "[\"B\"]"
        });
        var context = new DefaultHttpContext();
        context.Items["InstructorCode"] = "INST";

        var result = await SessionResultsEndpointHandlers.GetSessionResults(
            session.Id,
            context,
            CreateSessionRepository(session).Object,
            questionRepo,
            responseRepo);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<InstructorSurveyResultsResponse>>(result);
        Assert.Equal(2, ok.Value!.ParticipantCount);
        Assert.Equal(1, ok.Value.CompletedCount);
        Assert.Equal(2, ok.Value.Questions.Single().ResponseCount);
    }

    private static Session CreateSession(ResultsVisibility visibility) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Survey",
        Status = SessionStatus.Closed,
        ResultsVisibility = visibility
    };

    private static Mock<ISessionRepository> CreateSessionRepository(Session session)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(repo => repo.GetByIdAsync(session.Id)).ReturnsAsync(session);
        return repository;
    }
}
