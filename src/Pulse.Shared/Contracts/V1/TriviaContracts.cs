using Pulse.Shared.Models;

namespace Pulse.Shared.Contracts.V1;

public sealed record CreateTriviaGameRequest(
    string Title,
    TriviaCompetitionMode CompetitionMode,
    int PointsPerCorrectAnswer = 100,
    int MaximumTimeBonus = 50,
    int QuestionDurationSeconds = 20);

public sealed record AddTriviaQuestionRequest(Guid QuestionBankItemId);
public sealed record AddTriviaQuestionsRequest(QuestionBankQuery Filter);
public sealed record AddTriviaQuestionsResponse(int AddedCount, int AlreadyIncludedCount);
public sealed record CreateTriviaTeamRequest(string Name);
public sealed record JoinTriviaRequest(string DeviceId, string DisplayName, Guid? TeamId);
public sealed record JoinTriviaResponse(Guid GameId, Guid ParticipantId, string Title, TriviaCompetitionMode CompetitionMode);
public sealed record SubmitTriviaAnswerRequest(Guid ParticipantId, IReadOnlyList<string> Values);
public sealed record SubmitTriviaAnswerResponse(bool IsCorrect, int AwardedPoints);

public sealed record TriviaQuestionView(
    Guid Id,
    string Text,
    string Type,
    IReadOnlyList<string> Options,
    DateTime OpenedAt,
    int DurationSeconds);

public sealed record TriviaGameStateResponse(
    Guid Id,
    string Title,
    string JoinCode,
    TriviaCompetitionMode CompetitionMode,
    TriviaGameStatus Status,
    TriviaQuestionView? CurrentQuestion,
    IReadOnlyList<TriviaLeaderboardEntry> Leaderboard);

public sealed record InstructorTriviaStateResponse(
    TriviaGameStateResponse State,
    IReadOnlyList<TriviaQuestion> Questions,
    IReadOnlyList<TriviaTeam> Teams,
    int ParticipantCount);
