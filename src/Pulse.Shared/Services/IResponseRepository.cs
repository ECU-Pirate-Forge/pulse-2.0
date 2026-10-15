using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface IResponseRepository
{
    Response Upsert(Response response);
    void SubmitSurvey(IReadOnlyCollection<Response> responses, SurveySubmission submission);
    IEnumerable<Response> GetByQuestionId(Guid questionId);
    IEnumerable<Response> GetBySession(Guid sessionId);
    IEnumerable<Response> GetBySessionAndDevice(Guid sessionId, string deviceId);
    IEnumerable<SurveySubmission> GetSubmissionsBySession(Guid sessionId);
}
