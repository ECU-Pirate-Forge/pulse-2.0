using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public class ResponseRepository : IResponseRepository
{
    private readonly ILiteCollection<Response> _col;

    public ResponseRepository(LiteDatabase db)
    {
        _col = db.GetCollection<Response>("responses");
        _col.EnsureIndex("device_question", "$.DeviceId + $.QuestionId", unique: false);
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

    public IEnumerable<Response> GetByQuestionId(Guid questionId) =>
        _col.Find(r => r.QuestionId == questionId);
}
