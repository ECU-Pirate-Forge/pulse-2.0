using Pulse.Common.Services;

namespace Pulse.WebApi;

internal static class SurveyProgress
{
    public static int CountCompleted(
        Guid sessionId,
        IReadOnlyCollection<Guid> questionIds,
        IEnumerable<Pulse.Shared.Models.SurveySubmission> submissions,
        IResponseRepository responseRepo)
    {
        if (questionIds.Count == 0)
            return 0;

        var responsesByDevice = responseRepo.GetBySession(sessionId)
            .GroupBy(response => response.DeviceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(response => response.QuestionId).ToHashSet(),
                StringComparer.Ordinal);

        return submissions
            .Select(submission => submission.DeviceId)
            .Distinct(StringComparer.Ordinal)
            .Count(deviceId => responsesByDevice.TryGetValue(deviceId, out var answeredIds) &&
                questionIds.All(answeredIds.Contains));
    }
}
