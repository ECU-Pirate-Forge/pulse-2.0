using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Pulse.AppHost.DevTunnels;

public sealed record DevTunnelCommandResult(int ExitCode, string Output, string Error);

public interface IDevTunnelHost : IAsyncDisposable
{
    bool HasExited { get; }
}

public interface IDevTunnelCli
{
    Task<DevTunnelCommandResult> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments);
    IDevTunnelHost StartHost(string tunnelId);
}

public sealed class DevTunnelCli(string cliPath, ILogger logger) : IDevTunnelCli
{
    public async Task<DevTunnelCommandResult> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = Start(arguments);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new(process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    public IDevTunnelHost StartHost(string tunnelId) =>
        new HostProcess(Start(["host", tunnelId, "--host-header", "unchanged", "--origin-header", "unchanged"]), logger, tunnelId);

    private Process Start(string[] arguments)
    {
        var startInfo = new ProcessStartInfo(cliPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start devtunnel CLI '{cliPath}'.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Cannot start devtunnel CLI '{cliPath}'. Install the Microsoft devtunnel CLI, check DevTunnels:CliPath, and run 'devtunnel user login' before starting AppHost.",
                exception);
        }
    }

    private sealed class HostProcess : IDevTunnelHost
    {
        private readonly Process _process;
        private readonly Task _output;
        private readonly Task _error;
        public bool HasExited => _process.HasExited;

        public HostProcess(Process process, ILogger logger, string tunnelId)
        {
            _process = process;
            _output = DrainAsync(process.StandardOutput, logger, tunnelId, LogLevel.Information);
            _error = DrainAsync(process.StandardError, logger, tunnelId, LogLevel.Warning);
        }

        private static async Task DrainAsync(StreamReader reader, ILogger logger, string id, LogLevel level)
        {
            while (await reader.ReadLineAsync() is { } line)
                logger.Log(level, "devtunnel {TunnelId}: {Message}", id, line);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            await Task.WhenAll(_output, _error);
            _process.Dispose();
        }
    }
}
