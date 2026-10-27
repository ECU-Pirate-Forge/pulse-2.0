using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;

namespace Pulse.WebApi;

public static class TriviaEndpointHandlers
{
    public static IResult CreateGame(
        CreateTriviaGameRequest request,
        HttpContext context,
        TriviaRepository repository,
        IJoinCodeGenerator joinCodeGenerator)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");
        if (request.PointsPerCorrectAnswer < 0 ||
            request.MaximumTimeBonus < 0 ||
            request.QuestionDurationSeconds is < 5 or > 300)
        {
            return Results.BadRequest("Scoring values must be non-negative and duration must be between 5 and 300 seconds.");
        }

        var instructorCode = InstructorCode(context);
        if (instructorCode is null)
            return Results.Unauthorized();

        string joinCode;
        do
        {
            joinCode = joinCodeGenerator.Generate();
        } while (repository.GetGameByJoinCode(joinCode) is not null);

        var game = repository.InsertGame(new TriviaGame
        {
            Title = request.Title.Trim(),
            InstructorCode = instructorCode,
            JoinCode = joinCode,
            CompetitionMode = request.CompetitionMode,
            PointsPerCorrectAnswer = request.PointsPerCorrectAnswer,
            MaximumTimeBonus = request.MaximumTimeBonus,
            QuestionDurationSeconds = request.QuestionDurationSeconds
        });
        return Results.Created($"/api/trivia/{game.Id}", game);
    }

    public static IResult GetGames(HttpContext context, TriviaRepository repository)
    {
        var instructorCode = InstructorCode(context);
        return instructorCode is null
            ? Results.Unauthorized()
            : Results.Ok(repository.GetGames(instructorCode));
    }

    public static IResult DeleteGame(
        Guid gameId,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        return game is null || !repository.DeleteGame(gameId)
            ? Results.NotFound()
            : Results.NoContent();
    }

    public static IResult AddTeam(
        Guid gameId,
        CreateTriviaTeamRequest request,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        if (game.CompetitionMode != TriviaCompetitionMode.Team ||
            game.Status != TriviaGameStatus.Lobby ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest("Teams can only be added to a team game while it is in the lobby.");
        }

        return Results.Created(
            $"/api/trivia/{gameId}/teams",
            repository.InsertTeam(new TriviaTeam { GameId = gameId, Name = request.Name.Trim() }));
    }

    public static IResult AddQuestion(
        Guid gameId,
        AddTriviaQuestionRequest request,
        HttpContext context,
        TriviaRepository repository,
        IQuestionBankRepository bankRepository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        if (game.Status != TriviaGameStatus.Lobby)
            return Results.Conflict("Questions can only be added in the lobby.");

        var bankItem = bankRepository.GetById(request.QuestionBankItemId);
        if (bankItem is null ||
            bankItem.IsDraft || bankItem.ModerationStatus != QuestionModerationStatus.Approved ||
            !bankItem.TriviaEligible ||
            bankItem.CorrectAnswers.Count == 0)
        {
            return Results.BadRequest("Select an approved, trivia-eligible question with a correct answer.");
        }

        var question = CreateTriviaQuestion(bankItem, gameId, repository.GetQuestions(gameId).Count);
        return Results.Created($"/api/trivia/{gameId}/questions/{question.Id}", repository.InsertQuestion(question));
    }

    public static IResult AddFilteredQuestions(
        Guid gameId,
        AddTriviaQuestionsRequest request,
        HttpContext context,
        TriviaRepository repository,
        IQuestionBankRepository bankRepository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        if (game.Status != TriviaGameStatus.Lobby)
            return Results.Conflict("Questions can only be added in the lobby.");
        if (request.Filter is null)
            return Results.BadRequest("A question filter is required.");
        if (request.Filter.Validate() is { } validationError)
            return Results.BadRequest(validationError);

        var filter = request.Filter with
        {
            ModerationStatus = QuestionModerationStatus.Approved,
            TriviaEligible = true,
            IsDraft = false
        };
        var matchingItems = bankRepository.Search(filter).ToList();
        if (matchingItems.Any(item => item.CorrectAnswers.Count == 0))
            return Results.BadRequest("Every matching question must have a correct answer before adding them.");

        var existingItemIds = repository.GetQuestions(gameId)
            .Select(question => question.QuestionBankItemId)
            .ToHashSet();
        var addedCount = 0;
        var sortOrder = repository.GetQuestions(gameId).Count;
        foreach (var item in matchingItems)
        {
            if (!existingItemIds.Add(item.Id))
                continue;

            repository.InsertQuestion(CreateTriviaQuestion(item, gameId, sortOrder++));
            addedCount++;
        }

        return Results.Ok(new AddTriviaQuestionsResponse(
            addedCount,
            matchingItems.Count - addedCount));
    }

    public static IResult RemoveQuestion(
        Guid gameId,
        Guid questionId,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        if (game.Status != TriviaGameStatus.Lobby)
            return Results.Conflict("Questions can only be removed in the lobby.");
        return repository.DeleteQuestion(gameId, questionId)
            ? Results.NoContent()
            : Results.NotFound();
    }

    public static IResult Join(
        string joinCode,
        JoinTriviaRequest request,
        TriviaRepository repository,
        DeviceIdValidationService deviceIds)
    {
        var game = repository.GetGameByJoinCode(joinCode.Trim().ToUpperInvariant());
        if (game is null)
            return Results.NotFound("Trivia game not found.");
        if (game.Status == TriviaGameStatus.Finished)
            return Results.Conflict("This trivia game has finished.");
        if (!deviceIds.ValidateDeviceId(request.DeviceId).IsValid || string.IsNullOrWhiteSpace(request.DisplayName))
            return Results.BadRequest("A valid device ID and display name are required.");

        if (game.CompetitionMode == TriviaCompetitionMode.Team)
        {
            if (!request.TeamId.HasValue ||
                repository.GetTeams(game.Id).All(team => team.Id != request.TeamId))
            {
                return Results.BadRequest("Select a valid team.");
            }
        }

        var participant = repository.GetOrCreateParticipant(
            game.Id,
            request.DeviceId,
            request.DisplayName.Trim(),
            request.TeamId);
        return Results.Ok(new JoinTriviaResponse(
            game.Id,
            participant.Id,
            game.Title,
            game.CompetitionMode));
    }

    public static IResult GetJoinInfo(string joinCode, TriviaRepository repository)
    {
        var game = repository.GetGameByJoinCode(joinCode.Trim().ToUpperInvariant());
        if (game is null)
            return Results.NotFound();
        return Results.Ok(new
        {
            game.Id,
            game.Title,
            game.CompetitionMode,
            Teams = repository.GetTeams(game.Id)
        });
    }

    public static IResult OpenQuestion(
        Guid gameId,
        Guid questionId,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        var question = repository.GetQuestions(gameId).FirstOrDefault(item => item.Id == questionId);
        if (game is null || question is null)
            return Results.NotFound();
        if (game.Status is TriviaGameStatus.Finished)
            return Results.Conflict("This trivia game has finished.");

        game.CurrentQuestionId = questionId;
        game.QuestionOpenedAt = DateTime.UtcNow;
        game.Status = TriviaGameStatus.QuestionOpen;
        repository.UpdateGame(game);
        return Results.Ok(game);
    }

    public static IResult CloseQuestion(
        Guid gameId,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        if (game.Status != TriviaGameStatus.QuestionOpen)
            return Results.Conflict("No trivia question is open.");
        game.Status = TriviaGameStatus.QuestionClosed;
        repository.UpdateGame(game);
        return Results.Ok(game);
    }

    public static IResult FinishGame(
        Guid gameId,
        HttpContext context,
        TriviaRepository repository)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();
        game.Status = TriviaGameStatus.Finished;
        repository.UpdateGame(game);
        return Results.Ok(game);
    }

    public static IResult SubmitAnswer(
        Guid gameId,
        Guid questionId,
        SubmitTriviaAnswerRequest request,
        TriviaRepository repository,
        TriviaScoringService scoringService)
    {
        var game = repository.GetGame(gameId);
        var question = repository.GetQuestions(gameId).FirstOrDefault(item => item.Id == questionId);
        var participant = repository.GetParticipants(gameId).FirstOrDefault(item => item.Id == request.ParticipantId);
        if (game is null || question is null || participant is null)
            return Results.NotFound();
        if (game.Status != TriviaGameStatus.QuestionOpen ||
            game.CurrentQuestionId != questionId ||
            game.QuestionOpenedAt is null)
        {
            return Results.Conflict("This question is not accepting answers.");
        }
        if (repository.GetAnswer(questionId, participant.Id) is not null)
            return Results.Conflict("An answer has already been submitted.");

        var submittedAt = DateTime.UtcNow;
        var score = scoringService.Score(
            question.CorrectAnswers,
            request.Values,
            game.QuestionOpenedAt.Value,
            submittedAt,
            TimeSpan.FromSeconds(game.QuestionDurationSeconds),
            game.PointsPerCorrectAnswer,
            game.MaximumTimeBonus);
        if (!score.Accepted)
            return Results.Conflict("The answer arrived after the question deadline.");

        repository.InsertAnswer(new TriviaAnswer
        {
            GameId = gameId,
            QuestionId = questionId,
            ParticipantId = participant.Id,
            Values = [.. request.Values],
            IsCorrect = score.IsCorrect,
            AwardedPoints = score.Points,
            SubmittedAt = submittedAt
        });
        return Results.Ok(new SubmitTriviaAnswerResponse(score.IsCorrect, score.Points));
    }

    public static IResult GetState(
        Guid gameId,
        Guid participantId,
        TriviaRepository repository,
        TriviaScoringService scoringService)
    {
        var game = repository.GetGame(gameId);
        if (game is null || repository.GetParticipants(gameId).All(item => item.Id != participantId))
            return Results.NotFound();

        TriviaQuestionView? questionView = null;
        if (game.Status == TriviaGameStatus.QuestionOpen && game.CurrentQuestionId.HasValue)
        {
            var question = repository.GetQuestions(gameId)
                .FirstOrDefault(item => item.Id == game.CurrentQuestionId);
            if (question is not null)
            {
                questionView = new TriviaQuestionView(
                    question.Id,
                    question.Text,
                    question.Type.ToString(),
                    question.Options,
                    game.QuestionOpenedAt!.Value,
                    game.QuestionDurationSeconds);
            }
        }

        return Results.Ok(BuildState(game, questionView, repository, scoringService));
    }

    public static IResult GetInstructorState(
        Guid gameId,
        HttpContext context,
        TriviaRepository repository,
        TriviaScoringService scoringService)
    {
        var game = OwnedGame(gameId, context, repository);
        if (game is null)
            return Results.NotFound();

        TriviaQuestionView? questionView = null;
        if (game.CurrentQuestionId.HasValue)
        {
            var question = repository.GetQuestions(gameId)
                .FirstOrDefault(item => item.Id == game.CurrentQuestionId);
            if (question is not null)
            {
                questionView = new TriviaQuestionView(
                    question.Id,
                    question.Text,
                    question.Type.ToString(),
                    question.Options,
                    game.QuestionOpenedAt ?? DateTime.UtcNow,
                    game.QuestionDurationSeconds);
            }
        }
        return Results.Ok(new InstructorTriviaStateResponse(
            BuildState(game, questionView, repository, scoringService),
            repository.GetQuestions(gameId),
            repository.GetTeams(gameId),
            repository.GetParticipants(gameId).Count));
    }

    private static TriviaGameStateResponse BuildState(
        TriviaGame game,
        TriviaQuestionView? currentQuestion,
        TriviaRepository repository,
        TriviaScoringService scoringService)
    {
        var participants = repository.GetParticipants(game.Id);
        var answers = repository.GetAnswers(game.Id);
        var leaderboard = game.CompetitionMode == TriviaCompetitionMode.Team
            ? scoringService.BuildTeamLeaderboard(repository.GetTeams(game.Id), participants, answers)
            : scoringService.BuildIndividualLeaderboard(participants, answers);
        return new TriviaGameStateResponse(
            game.Id,
            game.Title,
            game.JoinCode,
            game.CompetitionMode,
            game.Status,
            currentQuestion,
            leaderboard);
    }

    private static TriviaGame? OwnedGame(
        Guid gameId,
        HttpContext context,
        TriviaRepository repository)
    {
        var code = InstructorCode(context);
        var game = repository.GetGame(gameId);
        return game is not null && string.Equals(game.InstructorCode, code, StringComparison.Ordinal)
            ? game
            : null;
    }

    private static string? InstructorCode(HttpContext context) =>
        context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();

    private static TriviaQuestion CreateTriviaQuestion(
        QuestionBankItem bankItem,
        Guid gameId,
        int sortOrder) => new()
    {
        GameId = gameId,
        QuestionBankItemId = bankItem.Id,
        Text = bankItem.Text,
        Type = bankItem.Type,
        Options = bankItem.Type == QuestionType.TrueFalse
            ? ["True", "False"]
            : [.. bankItem.Options],
        CorrectAnswers = [.. bankItem.CorrectAnswers],
        SortOrder = sortOrder
    };
}
