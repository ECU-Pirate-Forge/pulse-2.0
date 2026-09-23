using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public class ResponseRepository : IResponseRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Response> _col;
    private readonly ILiteCollection<SurveySubmission> _submissions;

    public ResponseRepository(LiteDatabase db)
    {
        _db = db;
        _col = db.GetCollection<Response>("responses");
        _submissions = db.GetCollection<SurveySubmission>("survey_submissions");
        RemoveDuplicateResponses();
        _col.DropIndex("device_question");
        _col.EnsureIndex("device_question", "$.DeviceId + $.QuestionId", unique: true);
        _submissions.EnsureIndex("session_device", "$.SessionId + $.DeviceId", unique: true);
    }

    public Response Upsert(Response response)
    {
        var existing = _col.FindOne(r =>
            r.DeviceId == response.DeviceId &&
            r.QuestionId == response.QuestionId);

        if (existing is not null)
        {
            response.Id = existing.Id;
            _col.Update(response);
        }
        else
        {
            _col.Insert(response);
        }

        return response;
    }

    public void SubmitSurvey(IReadOnlyCollection<Response> responses, SurveySubmission submission)
    {
        if (!_db.BeginTrans())
            throw new InvalidOperationException("Could not start the survey submission transaction.");

        try
        {
            foreach (var response in responses)
                Upsert(response);

            var existingSubmission = _submissions.FindOne(item =>
                item.SessionId == submission.SessionId &&
                item.DeviceId == submission.DeviceId);
            if (existingSubmission is not null)
            {
                submission.Id = existingSubmission.Id;
                _submissions.Update(submission);
            }
            else
            {
                _submissions.Insert(submission);
            }

            if (!_db.Commit())
                throw new InvalidOperationException("Could not commit the survey submission transaction.");
        }
        catch
        {
            _db.Rollback();
            throw;
        }
    }

    public IEnumerable<Response> GetByQuestionId(Guid questionId) =>
        _col.Find(r => r.QuestionId == questionId);

    public IEnumerable<Response> GetBySession(Guid sessionId) =>
        _col.Find(response => response.SessionId == sessionId);

    public IEnumerable<Response> GetBySessionAndDevice(Guid sessionId, string deviceId) =>
        _col.Find(response =>
            response.SessionId == sessionId &&
            response.DeviceId == deviceId);

    public IEnumerable<SurveySubmission> GetSubmissionsBySession(Guid sessionId) =>
        _submissions.Find(submission => submission.SessionId == sessionId);

    private void RemoveDuplicateResponses()
    {
        var duplicateGroups = _col.FindAll()
            .GroupBy(response => (response.DeviceId, response.QuestionId))
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var duplicate in group
                .OrderByDescending(response => response.SubmittedAt)
                .Skip(1))
            {
                _col.Delete(duplicate.Id);
            }
        }
    }
}
