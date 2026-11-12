using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class TriviaEndpointTests
{
    [Fact]
    public void AddQuestion_DoesNotAddTheSameBankQuestionTwice()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var bank = new QuestionBankRepository(database);
        var game = repository.InsertGame(Game());
        var bankQuestion = bank.Insert(BankQuestion("IP address basics"));
        var request = new AddTriviaQuestionRequest(bankQuestion.Id);

        var first = TriviaEndpointHandlers.AddQuestion(
            game.Id, request, InstructorContext("INST"), repository, bank);
        var repeated = TriviaEndpointHandlers.AddQuestion(
            game.Id, request, InstructorContext("INST"), repository, bank);

        Assert.IsType<Created<TriviaQuestion>>(first);
        Assert.IsType<Conflict<string>>(repeated);
        Assert.Single(repository.GetQuestions(game.Id));
    }

    [Fact]
    public void AddFilteredQuestions_AddsAllApprovedEligibleMatchesOnce()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var bank = new QuestionBankRepository(database);
        var game = repository.InsertGame(Game());
        var matchingQuestions = new[]
        {
            BankQuestion("IP address basics"),
            BankQuestion("IP routing")
        };
        foreach (var question in matchingQuestions)
            bank.Insert(question);
        bank.Insert(BankQuestion("Unrelated question"));
        bank.Insert(BankQuestion("IP pending", QuestionModerationStatus.Pending));

        var request = new AddTriviaQuestionsRequest(new QuestionBankQuery(Text: "IP"));
        var first = TriviaEndpointHandlers.AddFilteredQuestions(
            game.Id, request, InstructorContext("INST"), repository, bank);
        var repeated = TriviaEndpointHandlers.AddFilteredQuestions(
            game.Id, request, InstructorContext("INST"), repository, bank);

        var firstResponse = Assert.IsType<Ok<AddTriviaQuestionsResponse>>(first).Value!;
        var repeatedResponse = Assert.IsType<Ok<AddTriviaQuestionsResponse>>(repeated).Value!;
        Assert.Equal(2, firstResponse.AddedCount);
        Assert.Equal(0, firstResponse.AlreadyIncludedCount);
        Assert.Equal(0, repeatedResponse.AddedCount);
        Assert.Equal(2, repeatedResponse.AlreadyIncludedCount);
        Assert.Equal(2, repository.GetQuestions(game.Id).Count);
    }

    [Fact]
    public void RemoveQuestion_RemovesOnlyTheOwnedLobbyQuestionAndResequences()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = repository.InsertGame(Game());
        var removed = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            SortOrder = 0,
            Text = "Remove me"
        });
        var remaining = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            SortOrder = 1,
            Text = "Keep me"
        });

        var result = TriviaEndpointHandlers.RemoveQuestion(
            game.Id, removed.Id, InstructorContext("INST"), repository);

        Assert.IsType<NoContent>(result);
        var question = Assert.Single(repository.GetQuestions(game.Id));
        Assert.Equal(remaining.Id, question.Id);
        Assert.Equal(0, question.SortOrder);
        Assert.IsType<NotFound>(TriviaEndpointHandlers.RemoveQuestion(
            Guid.NewGuid(), remaining.Id, InstructorContext("INST"), repository));
        game.Status = TriviaGameStatus.QuestionOpen;
        repository.UpdateGame(game);
        Assert.IsType<Conflict<string>>(TriviaEndpointHandlers.RemoveQuestion(
            game.Id, remaining.Id, InstructorContext("INST"), repository));
        Assert.Single(repository.GetQuestions(game.Id));
    }

    [Fact]
    public void DeleteGame_DeletesOwnedGameAndItsRelatedRecords()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = repository.InsertGame(Game());
        var team = repository.InsertTeam(new TriviaTeam { GameId = game.Id, Name = "Gold" });
        var participant = repository.GetOrCreateParticipant(game.Id, "device", "Ada", team.Id);
        var question = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            Text = "Choose"
        });
        repository.InsertAnswer(new TriviaAnswer
        {
            GameId = game.Id,
            QuestionId = question.Id,
            ParticipantId = participant.Id
        });

        var forbidden = TriviaEndpointHandlers.DeleteGame(
            game.Id, InstructorContext("OTHER"), repository);
        var deleted = TriviaEndpointHandlers.DeleteGame(
            game.Id, InstructorContext("INST"), repository);

        Assert.IsType<NotFound>(forbidden);
        Assert.IsType<NoContent>(deleted);
        Assert.Null(repository.GetGame(game.Id));
        Assert.Empty(repository.GetQuestions(game.Id));
        Assert.Empty(repository.GetTeams(game.Id));
        Assert.Empty(repository.GetParticipants(game.Id));
        Assert.Empty(repository.GetAnswers(game.Id));
    }

    [Fact]
    public void Join_WithSameDevice_ReconnectsToExistingParticipant()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = repository.InsertGame(Game());
        var request = new JoinTriviaRequest(Guid.NewGuid().ToString(), "Ada", null);

        var first = TriviaEndpointHandlers.Join(game.JoinCode, request, repository, new DeviceIdValidationService());
        var second = TriviaEndpointHandlers.Join(game.JoinCode, request, repository, new DeviceIdValidationService());

        var firstResponse = Assert.IsType<Ok<JoinTriviaResponse>>(first).Value!;
        var secondResponse = Assert.IsType<Ok<JoinTriviaResponse>>(second).Value!;
        Assert.Equal(firstResponse.ParticipantId, secondResponse.ParticipantId);
        Assert.Single(repository.GetParticipants(game.Id));
    }

    [Fact]
    public void Join_TeamGameRequiresInstructorDefinedTeam()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = Game();
        game.CompetitionMode = TriviaCompetitionMode.Team;
        repository.InsertGame(game);
        var team = repository.InsertTeam(new TriviaTeam { GameId = game.Id, Name = "Gold" });
        var deviceId = Guid.NewGuid().ToString();

        var invalid = TriviaEndpointHandlers.Join(
            game.JoinCode,
            new JoinTriviaRequest(deviceId, "Ada", Guid.NewGuid()),
            repository,
            new DeviceIdValidationService());
        var valid = TriviaEndpointHandlers.Join(
            game.JoinCode,
            new JoinTriviaRequest(deviceId, "Ada", team.Id),
            repository,
            new DeviceIdValidationService());

        Assert.IsType<BadRequest<string>>(invalid);
        Assert.Equal(team.Id, repository.GetParticipants(game.Id).Single().TeamId);
        Assert.IsType<Ok<JoinTriviaResponse>>(valid);
    }

    [Fact]
    public void SubmitAnswer_UsesServerClockAndRejectsLateAnswer()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = Game();
        game.Status = TriviaGameStatus.QuestionOpen;
        game.QuestionOpenedAt = DateTime.UtcNow.AddMinutes(-1);
        game.QuestionDurationSeconds = 10;
        repository.InsertGame(game);
        var question = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            Text = "Choose",
            Options = ["A", "B"],
            CorrectAnswers = ["A"]
        });
        game.CurrentQuestionId = question.Id;
        repository.UpdateGame(game);
        var participant = repository.GetOrCreateParticipant(
            game.Id,
            Guid.NewGuid().ToString(),
            "Ada",
            null);

        var result = TriviaEndpointHandlers.SubmitAnswer(
            game.Id,
            question.Id,
            new SubmitTriviaAnswerRequest(participant.Id, ["A"]),
            repository,
            new TriviaScoringService());

        Assert.IsType<Conflict<string>>(result);
        Assert.Empty(repository.GetAnswers(game.Id));
    }

    [Fact]
    public void SubmitAnswer_AllowsOnlyOneServerScoredAnswer()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = Game();
        game.Status = TriviaGameStatus.QuestionOpen;
        game.QuestionOpenedAt = DateTime.UtcNow;
        repository.InsertGame(game);
        var question = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            Text = "Choose",
            Options = ["A", "B"],
            CorrectAnswers = ["A"]
        });
        game.CurrentQuestionId = question.Id;
        repository.UpdateGame(game);
        var participant = repository.GetOrCreateParticipant(
            game.Id,
            Guid.NewGuid().ToString(),
            "Ada",
            null);
        var request = new SubmitTriviaAnswerRequest(participant.Id, ["A"]);

        var first = TriviaEndpointHandlers.SubmitAnswer(
            game.Id, question.Id, request, repository, new TriviaScoringService());
        var second = TriviaEndpointHandlers.SubmitAnswer(
            game.Id, question.Id, request, repository, new TriviaScoringService());

        var response = Assert.IsType<Ok<SubmitTriviaAnswerResponse>>(first).Value!;
        Assert.True(response.IsCorrect);
        Assert.InRange(response.AwardedPoints, game.PointsPerCorrectAnswer, 150);
        Assert.IsType<Conflict<string>>(second);
        Assert.Single(repository.GetAnswers(game.Id));
    }

    [Fact]
    public void StudentState_DoesNotExposeCorrectAnswers()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new TriviaRepository(database);
        var game = Game();
        game.Status = TriviaGameStatus.QuestionOpen;
        game.QuestionOpenedAt = DateTime.UtcNow;
        repository.InsertGame(game);
        var question = repository.InsertQuestion(new TriviaQuestion
        {
            GameId = game.Id,
            Text = "Choose",
            Options = ["A", "B"],
            CorrectAnswers = ["A"]
        });
        game.CurrentQuestionId = question.Id;
        repository.UpdateGame(game);
        var participant = repository.GetOrCreateParticipant(
            game.Id,
            Guid.NewGuid().ToString(),
            "Ada",
            null);

        var result = TriviaEndpointHandlers.GetState(
            game.Id,
            participant.Id,
            repository,
            new TriviaScoringService());
        var state = Assert.IsType<Ok<TriviaGameStateResponse>>(result).Value!;
        var json = System.Text.Json.JsonSerializer.Serialize(state);

        Assert.NotNull(state.CurrentQuestion);
        Assert.DoesNotContain("CorrectAnswers", json, StringComparison.OrdinalIgnoreCase);
    }

    private static TriviaGame Game() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Trivia",
        InstructorCode = "INST",
        JoinCode = "ABC123",
        CompetitionMode = TriviaCompetitionMode.Individual
    };

    private static QuestionBankItem BankQuestion(
        string text,
        QuestionModerationStatus moderationStatus = QuestionModerationStatus.Approved) => new()
    {
        Text = text,
        Type = QuestionType.MultipleChoice,
        Options = ["Yes", "No"],
        CorrectAnswers = ["Yes"],
        ModerationStatus = moderationStatus,
        TriviaEligible = true
    };

    private static DefaultHttpContext InstructorContext(string instructorCode)
    {
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }
}
