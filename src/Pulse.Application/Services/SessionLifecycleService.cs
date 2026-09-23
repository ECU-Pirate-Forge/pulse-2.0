using Pulse.Shared.Models;

namespace Pulse.Application.Services;

public static class SessionLifecycleService
{
    public static bool CanTransition(SessionStatus current, SessionStatus next) =>
        (current, next) switch
        {
            (SessionStatus.Draft, SessionStatus.Active) => true,
            (SessionStatus.Active, SessionStatus.Closed) => true,
            _ => false
        };
}
