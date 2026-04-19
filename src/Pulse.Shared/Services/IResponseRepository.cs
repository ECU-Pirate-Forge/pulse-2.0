using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface IResponseRepository
{
    Response Upsert(Response response);
    IEnumerable<Response> GetByQuestionId(Guid questionId);
}
