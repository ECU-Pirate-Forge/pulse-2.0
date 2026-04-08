using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Shared.Services;

public class ResponseRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Response> _col;

    public ResponseRepository(LiteDatabase db)
    {
        _db = db;
        _col = _db.GetCollection<Response>("responses");
        _col.EnsureIndex("ix_questionId_deviceId", "$.QuestionId + '|' + $.DeviceId", true);
    }

    public Response UpsertByQuestionAndDevice(string questionId, string deviceId, string value)
    {
        if (!DeviceIdValidator.IsValid(deviceId))
        {
            throw new ArgumentException("Invalid DeviceId format.");
        }

        var existing = _col.FindOne(r => r.QuestionId == questionId && r.DeviceId == deviceId);

        if (existing != null)
        {
            existing.Value = value;
            _col.Update(existing);
            return existing;
        }

        var response = new Response
        {
            Id = Guid.NewGuid().ToString(),
            QuestionId = questionId,
            DeviceId = deviceId,
            Value = value
        };

        _col.Insert(response);
        return response;
    }
}