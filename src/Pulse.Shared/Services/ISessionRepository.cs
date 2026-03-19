using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public interface ISessionRepository
{
	Session Insert(Session session);
	Session? GetById(Guid id);
}
