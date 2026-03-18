using Pulse.Shared.Models;

public interface ISessionRepository
{
	Session Insert(Session session);
	Session? GetById(Guid id);
}
