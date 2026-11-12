using Microsoft.AspNetCore.SignalR.Client;
using Pulse.Shared.Contracts.V1;

namespace Pulse.Shared.Services;

public sealed class SessionRealtimeConnectionFactory(
    string configuredBaseUrl,
    Func<HttpMessageHandler> messageHandlerFactory)
{
    public SessionRealtimeConnection Create(
        Func<SessionProgressUpdate, Task> submissionUpdated,
        Func<Task> sessionClosed,
        Func<Task> resultsRevealed,
        Func<Task>? questionsUpdated = null)
    {
        var baseUrl = configuredBaseUrl.StartsWith("https+http://", StringComparison.OrdinalIgnoreCase)
            ? $"http://{configuredBaseUrl["https+http://".Length..]}"
            : configuredBaseUrl;

        var connection = new HubConnectionBuilder()
            .WithUrl(
                $"{baseUrl.TrimEnd('/')}/hubs/session",
                options => options.HttpMessageHandlerFactory =
                    _ => messageHandlerFactory())
            .WithAutomaticReconnect()
            .Build();

        connection.On<SessionProgressUpdate>("SubmissionUpdated", submissionUpdated);
        connection.On("SessionClosed", sessionClosed);
        connection.On("ResultsRevealed", resultsRevealed);
        if (questionsUpdated is not null)
            connection.On("QuestionsUpdated", questionsUpdated);

        return new SessionRealtimeConnection(connection);
    }
}

public sealed class SessionRealtimeConnection(HubConnection connection) : IAsyncDisposable
{
    public async Task StartAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await connection.StartAsync(cancellationToken);
        await connection.InvokeAsync("JoinSession", sessionId, cancellationToken);
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
