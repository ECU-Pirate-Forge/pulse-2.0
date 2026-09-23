using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public sealed class TriviaRepository
{
    private readonly ILiteCollection<TriviaGame> games;
    private readonly ILiteCollection<TriviaQuestion> questions;
    private readonly ILiteCollection<TriviaTeam> teams;
    private readonly ILiteCollection<TriviaParticipant> participants;
    private readonly ILiteCollection<TriviaAnswer> answers;

    public TriviaRepository(LiteDatabase database)
    {
        games = database.GetCollection<TriviaGame>("trivia_games");
        questions = database.GetCollection<TriviaQuestion>("trivia_questions");
        teams = database.GetCollection<TriviaTeam>("trivia_teams");
        participants = database.GetCollection<TriviaParticipant>("trivia_participants");
        answers = database.GetCollection<TriviaAnswer>("trivia_answers");
        games.EnsureIndex(game => game.JoinCode, unique: true);
        questions.EnsureIndex(question => question.GameId);
        teams.EnsureIndex(team => team.GameId);
        participants.EnsureIndex(participant => participant.GameId);
        participants.EnsureIndex(participant => participant.DeviceId);
        answers.EnsureIndex(answer => answer.GameId);
        participants.DropIndex("game_device");
        answers.DropIndex("question_participant");
        foreach (var participant in participants.FindAll().ToList())
        {
            participant.GameDeviceKey = DeviceKey(participant.GameId, participant.DeviceId);
            participants.Update(participant);
        }
        foreach (var answer in answers.FindAll().ToList())
        {
            answer.QuestionParticipantKey = AnswerKey(answer.QuestionId, answer.ParticipantId);
            answers.Update(answer);
        }
        participants.EnsureIndex(participant => participant.GameDeviceKey, unique: true);
        answers.EnsureIndex(answer => answer.QuestionParticipantKey, unique: true);
    }

    public TriviaGame InsertGame(TriviaGame game)
    {
        games.Insert(game);
        return game;
    }

    public bool UpdateGame(TriviaGame game) => games.Update(game);
    public TriviaGame? GetGame(Guid id) => games.FindById(id);
    public TriviaGame? GetGameByJoinCode(string joinCode) =>
        games.FindOne(game => game.JoinCode == joinCode);
    public IReadOnlyList<TriviaGame> GetGames(string instructorCode) =>
        games.Find(game => game.InstructorCode == instructorCode)
            .OrderByDescending(game => game.CreatedAt)
            .ToList();

    public TriviaTeam InsertTeam(TriviaTeam team)
    {
        teams.Insert(team);
        return team;
    }

    public IReadOnlyList<TriviaTeam> GetTeams(Guid gameId) =>
        teams.Find(team => team.GameId == gameId).ToList();

    public TriviaQuestion InsertQuestion(TriviaQuestion question)
    {
        questions.Insert(question);
        return question;
    }

    public IReadOnlyList<TriviaQuestion> GetQuestions(Guid gameId) =>
        questions.Find(question => question.GameId == gameId)
            .OrderBy(question => question.SortOrder)
            .ToList();

    public TriviaParticipant GetOrCreateParticipant(
        Guid gameId,
        string deviceId,
        string displayName,
        Guid? teamId)
    {
        var existing = participants.FindOne(participant =>
            participant.GameId == gameId && participant.DeviceId == deviceId);
        if (existing is not null)
            return existing;

        var participant = new TriviaParticipant
        {
            GameId = gameId,
            DeviceId = deviceId,
            DisplayName = displayName,
            TeamId = teamId,
            GameDeviceKey = DeviceKey(gameId, deviceId)
        };
        participants.Insert(participant);
        return participant;
    }

    public IReadOnlyList<TriviaParticipant> GetParticipants(Guid gameId) =>
        participants.Find(participant => participant.GameId == gameId).ToList();

    public TriviaAnswer? GetAnswer(Guid questionId, Guid participantId) =>
        answers.FindOne(answer =>
            answer.QuestionId == questionId && answer.ParticipantId == participantId);

    public TriviaAnswer InsertAnswer(TriviaAnswer answer)
    {
        answer.QuestionParticipantKey = AnswerKey(answer.QuestionId, answer.ParticipantId);
        answers.Insert(answer);
        return answer;
    }

    public IReadOnlyList<TriviaAnswer> GetAnswers(Guid gameId) =>
        answers.Find(answer => answer.GameId == gameId).ToList();

    private static string DeviceKey(Guid gameId, string deviceId) => $"{gameId:N}:{deviceId}";
    private static string AnswerKey(Guid questionId, Guid participantId) =>
        $"{questionId:N}:{participantId:N}";
}
