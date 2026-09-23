using Microsoft.AspNetCore.SignalR;
using Pulse.Shared.Contracts.V1;

namespace Pulse.WebApi;

public interface ISessionClient
{
    Task SubmissionUpdated(SessionProgressUpdate update);
    Task SessionClosed();
    Task ResultsRevealed();
}

public sealed class SessionHub : Hub<ISessionClient>
{
    public Task JoinSession(Guid sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    internal static string GroupName(Guid sessionId) => $"session:{sessionId:N}";
}

public interface ISessionEventPublisher
{
    Task PublishSubmissionUpdatedAsync(
        Guid sessionId,
        SessionProgressUpdate update,
        CancellationToken cancellationToken = default);

    Task PublishSessionClosedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task PublishResultsRevealedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class SignalRSessionEventPublisher(IHubContext<SessionHub, ISessionClient> hubContext)
    : ISessionEventPublisher
{
    public Task PublishSubmissionUpdatedAsync(
        Guid sessionId,
        SessionProgressUpdate update,
        CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(SessionHub.GroupName(sessionId))
            .SubmissionUpdated(update);

    public Task PublishSessionClosedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(SessionHub.GroupName(sessionId))
            .SessionClosed();

    public Task PublishResultsRevealedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(SessionHub.GroupName(sessionId))
            .ResultsRevealed();
}
