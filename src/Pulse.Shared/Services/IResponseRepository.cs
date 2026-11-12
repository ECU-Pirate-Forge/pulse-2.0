using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface IResponseRepository
{
    Response Upsert(Response response);
    void SubmitSurvey(IReadOnlyCollection<Response> responses, SurveySubmission submission);
    /// <summary>Stores a submission only if the persisted survey is still active in the expected run.</summary>
    bool TrySubmitSurvey(IReadOnlyCollection<Response> responses, SurveySubmission submission, int expectedResetVersion);
    IEnumerable<Response> GetByQuestionId(Guid questionId);
    IEnumerable<Response> GetBySession(Guid sessionId);
    IEnumerable<Response> GetBySessionAndDevice(Guid sessionId, string deviceId);
    IEnumerable<SurveySubmission> GetSubmissionsBySession(Guid sessionId);
}
