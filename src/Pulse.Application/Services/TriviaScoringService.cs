using Pulse.Shared.Models;

namespace Pulse.Application.Services;

public sealed class TriviaScoringService
{
    public TriviaScoreResult Score(
        IReadOnlyCollection<string> correctAnswers,
        IReadOnlyCollection<string> submittedAnswers,
        DateTime openedAt,
        DateTime submittedAt,
        TimeSpan duration,
        int basePoints,
        int maximumTimeBonus)
    {
        openedAt = openedAt.ToUniversalTime();
        submittedAt = submittedAt.ToUniversalTime();
        if (duration <= TimeSpan.Zero ||
            submittedAt < openedAt ||
            submittedAt > openedAt.Add(duration))
        {
            return new TriviaScoreResult(false, false, 0);
        }

        var isCorrect = correctAnswers.Count == submittedAnswers.Count &&
            new HashSet<string>(correctAnswers, StringComparer.Ordinal)
                .SetEquals(submittedAnswers);
        if (!isCorrect)
            return new TriviaScoreResult(true, false, 0);

        var elapsedRatio = (submittedAt - openedAt).TotalMilliseconds / duration.TotalMilliseconds;
        var bonus = (int)Math.Floor(maximumTimeBonus * Math.Clamp(1d - elapsedRatio, 0d, 1d));
        return new TriviaScoreResult(true, true, basePoints + bonus);
    }

    public IReadOnlyList<TriviaLeaderboardEntry> BuildIndividualLeaderboard(
        IEnumerable<TriviaParticipant> participants,
        IEnumerable<TriviaAnswer> answers)
    {
        var scores = answers
            .GroupBy(answer => answer.ParticipantId)
            .ToDictionary(group => group.Key, group => group.Sum(answer => answer.AwardedPoints));
        return Rank(participants.Select(participant => (
            participant.DisplayName,
            scores.GetValueOrDefault(participant.Id))));
    }

    public IReadOnlyList<TriviaLeaderboardEntry> BuildTeamLeaderboard(
        IEnumerable<TriviaTeam> teams,
        IEnumerable<TriviaParticipant> participants,
        IEnumerable<TriviaAnswer> answers)
    {
        var participantTeams = participants
            .Where(participant => participant.TeamId.HasValue)
            .ToDictionary(participant => participant.Id, participant => participant.TeamId!.Value);
        var scores = answers
            .Where(answer => participantTeams.ContainsKey(answer.ParticipantId))
            .GroupBy(answer => participantTeams[answer.ParticipantId])
            .ToDictionary(group => group.Key, group => group.Sum(answer => answer.AwardedPoints));
        return Rank(teams.Select(team => (team.Name, scores.GetValueOrDefault(team.Id))));
    }

    private static IReadOnlyList<TriviaLeaderboardEntry> Rank(
        IEnumerable<(string Name, int Score)> values)
    {
        var ordered = values
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new List<TriviaLeaderboardEntry>(ordered.Count);
        var previousScore = int.MinValue;
        var rank = 0;
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Score != previousScore)
            {
                rank = index + 1;
                previousScore = ordered[index].Score;
            }
            result.Add(new TriviaLeaderboardEntry(rank, ordered[index].Name, ordered[index].Score));
        }
        return result;
    }
}
