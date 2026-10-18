using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Pulse.AppHost.DevTunnels;

public sealed class DevTunnelManager(
    DevTunnelOptions options,
    IDevTunnelCli cli,
    HttpClient httpClient,
    ILogger logger) : IAsyncDisposable
{
    private readonly Dictionary<string, IDevTunnelHost> _ownedHosts = [];
    private readonly List<FileStream> _locks = [];
    private readonly Dictionary<string, Uri> _publicUrls = [];
    private readonly Dictionary<string, string> _probeErrors = [];
    public IReadOnlyDictionary<string, Uri> PublicUrls => _publicUrls;
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public bool IsHealthy { get; private set; }
    public ILogger Logger { private get; set; } = logger;

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        if (!options.Enabled)
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));
        try
        {
            var user = Parse(Check(await cli.ExecuteAsync(timeout.Token, "user", "show", "--json"), "user show"));
            if (!user.TryGetProperty("username", out var username) || string.IsNullOrWhiteSpace(username.GetString()))
                throw new InvalidOperationException("Sign in with 'devtunnel user login' before starting AppHost.");

            foreach (var (name, service) in options.Services)
            {
                AcquireLock(service.TunnelId);
                await PrepareServiceAsync(name, service, timeout.Token);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Dev tunnel provisioning timed out. Check connectivity, cached devtunnel sign-in, and DevTunnels:StartupTimeoutSeconds.", exception);
        }
    }

    private void AcquireLock(string tunnelId)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse", "DevTunnels");
        Directory.CreateDirectory(directory);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tunnelId)));
        try
        {
            _locks.Add(new FileStream(Path.Combine(directory, $"{key}.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Tunnel '{tunnelId}' is already managed by another local AppHost. Stop that AppHost or use different DevTunnels service IDs.", exception);
        }
    }

    private async Task PrepareServiceAsync(string name, DevTunnelServiceOptions service, CancellationToken cancellationToken)
    {
        var result = await cli.ExecuteAsync(cancellationToken, "show", service.TunnelId, "--json");
        if (result.ExitCode == 2)
        {
            Logger.LogInformation("Creating persistent anonymous tunnel {TunnelId}", service.TunnelId);
            var created = await cli.ExecuteAsync(cancellationToken, "create", service.TunnelId, "--allow-anonymous", "--json");
            // A competing creator may have won after our initial read. Re-read and validate instead of overwriting it.
            if (created.ExitCode != 1)
                Check(created, $"create {service.TunnelId}");
            result = await cli.ExecuteAsync(cancellationToken, "show", service.TunnelId, "--json");
        }

        var tunnel = ReadTunnel(Check(result, $"show {service.TunnelId}"));
        var running = tunnel.GetProperty("hostConnections").GetInt32() > 0;
        var ports = tunnel.TryGetProperty("ports", out var configuredPorts)
            ? configuredPorts.EnumerateArray().ToArray()
            : [];
        if (ports.Any(p => p.GetProperty("portNumber").GetInt32() != service.Port))
            throw new InvalidOperationException(
                $"Tunnel '{service.TunnelId}' has unrelated ports. Use a dedicated one-port Pulse tunnel; AppHost will not delete existing ports.");

        var port = ports.SingleOrDefault();
        if (port.ValueKind == JsonValueKind.Undefined)
        {
            if (running)
                throw new InvalidOperationException($"Running tunnel '{service.TunnelId}' does not forward configured port {service.Port}. Stop its host and correct the mapping.");
            Check(await cli.ExecuteAsync(cancellationToken, "port", "create", service.TunnelId,
                "--port-number", service.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--protocol", "http", "--host-header", "unchanged", "--origin-header", "unchanged", "--json"), $"port create {service.TunnelId}");
        }
        else if (port.GetProperty("protocol").GetString() != "http")
        {
            throw new InvalidOperationException($"Tunnel '{service.TunnelId}' must forward port {service.Port} using protocol http. AppHost will not repoint an existing tunnel.");
        }
        else if (!running)
        {
            Check(await cli.ExecuteAsync(cancellationToken, "port", "update", service.TunnelId,
                "--port-number", service.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--host-header", "unchanged", "--origin-header", "unchanged", "--json"), "port update");
        }

        var access = Parse(Check(await cli.ExecuteAsync(cancellationToken, "access", "list", service.TunnelId, "--json"), "access list"))
            .GetProperty("accessControlEntries");
        RejectDenyRules(access, service.TunnelId);
        var portAccess = Parse(Check(await cli.ExecuteAsync(cancellationToken, "access", "list", service.TunnelId,
            "--port-number", service.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--json"), "port access list"))
            .GetProperty("accessControlEntries");
        RejectDenyRules(portAccess, service.TunnelId);

        if (!HasPersistentAnonymousAccess(access))
        {
            if (running)
                throw new InvalidOperationException($"Running tunnel '{service.TunnelId}' lacks persistent anonymous connect access. Stop its host before AppHost configures access.");
            Check(await cli.ExecuteAsync(cancellationToken, "access", "create", service.TunnelId, "--anonymous", "--json"), "access create");
        }

        if (running)
            Logger.LogInformation("Reusing active tunnel {TunnelId}; its local target will be verified before declaring readiness", service.TunnelId);
        else
            _ownedHosts.Add(name, cli.StartHost(service.TunnelId));

        // A new port's public address is allocated by the service only after its host connects.
        string? url;
        do
        {
            tunnel = ReadTunnel(Check(await cli.ExecuteAsync(cancellationToken, "show", service.TunnelId, "--json"), "show"));
            port = tunnel.GetProperty("ports").EnumerateArray().Single(p => p.GetProperty("portNumber").GetInt32() == service.Port);
            url = port.TryGetProperty("portUri", out var portUri) ? portUri.GetString() : null;
            if (url is null)
            {
                if (_ownedHosts.TryGetValue(name, out var host) && host.HasExited)
                    throw new InvalidOperationException($"devtunnel host for '{name}' exited before its public URL was allocated. Check the host log.");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        } while (url is null);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var publicUrl) ||
            publicUrl.Scheme != Uri.UriSchemeHttps || !publicUrl.Host.EndsWith(".devtunnels.ms", StringComparison.OrdinalIgnoreCase) ||
            publicUrl.AbsolutePath != "/" || !publicUrl.IsDefaultPort || !string.IsNullOrEmpty(publicUrl.UserInfo) ||
            !string.IsNullOrEmpty(publicUrl.Query) || !string.IsNullOrEmpty(publicUrl.Fragment))
            throw new InvalidOperationException($"Tunnel '{service.TunnelId}' returned an invalid HTTPS portUri.");
        _publicUrls.Add(name, publicUrl);
        Logger.LogInformation("Public {Service} URL: {Url}", name, publicUrl);
    }

    private static bool HasPersistentAnonymousAccess(JsonElement entries) =>
        entries.EnumerateArray().Any(entry =>
            entry.GetProperty("type").GetString() == "Anonymous" &&
            entry.GetProperty("scopes").EnumerateArray().Any(scope => scope.GetString() == "connect") &&
            (!entry.TryGetProperty("expiration", out var expiry) || expiry.ValueKind == JsonValueKind.Null));

    private static void RejectDenyRules(JsonElement entries, string id)
    {
        if (entries.EnumerateArray().Any(entry =>
            (entry.TryGetProperty("isDeny", out var deny) && deny.ValueKind == JsonValueKind.True) ||
            (entry.TryGetProperty("deny", out deny) && deny.ValueKind == JsonValueKind.True)))
            throw new InvalidOperationException($"Tunnel '{id}' has deny rules. Resolve its access policy explicitly; AppHost will not remove deny rules.");
    }

    private static JsonElement Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("devtunnel returned malformed JSON. Update the CLI and retry.", exception);
        }
    }

    private static JsonElement ReadTunnel(string json)
    {
        var root = Parse(json);
        if (!root.TryGetProperty("tunnel", out var tunnel) ||
            !tunnel.TryGetProperty("hostConnections", out _))
            throw new InvalidOperationException("devtunnel show returned unsupported metadata. Update the CLI and retry.");
        return tunnel;
    }

    private static string Check(DevTunnelCommandResult result, string command)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"devtunnel {command} failed (exit {result.ExitCode}): {result.Error.Trim()}. Check 'devtunnel user login', ownership and network access.");
        return result.Output;
    }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));
        try
        {
            var pending = options.Services.Keys.ToHashSet();
            while (pending.Count > 0)
            {
                foreach (var name in pending.ToArray())
                {
                    if (await ProbeAsync(name, timeout.Token))
                    {
                        pending.Remove(name);
                        continue;
                    }
                    if (_ownedHosts.TryGetValue(name, out var host) && host.HasExited)
                        throw new InvalidOperationException($"devtunnel host for '{name}' exited during startup. Check the host log and active-host conflicts.");
                }
                if (pending.Count > 0)
                    await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            }
            IsHealthy = true;
            Logger.LogInformation("All Pulse public dev tunnels are ready and target this AppHost.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Public tunnel readiness timed out. Check resource health, Host/Origin preservation, and whether a reused host targets this machine. " +
                string.Join("; ", _probeErrors.Select(pair => $"{pair.Key}: {pair.Value}")), exception);
        }
    }

    private async Task<bool> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        var path = name == "scalar" ? "/" : "/.well-known/pulse-dev-tunnel";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_publicUrls[name], path));
        request.Headers.Add("X-Tunnel-Skip-AntiPhishing-Page", "true");
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var verified = response.IsSuccessStatusCode && HasExpectedIdentity(name, body);
            if (verified)
                _probeErrors.Remove(name);
            else
                _probeErrors[name] = $"HTTP {(int)response.StatusCode}, identity/origin not verified";
            return verified;
        }
        catch (HttpRequestException exception)
        {
            _probeErrors[name] = exception.Message;
            Logger.LogDebug(exception, "Public tunnel probe for {Service} could not connect", name);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _probeErrors[name] = "HTTP probe timeout";
            Logger.LogDebug("Public tunnel probe for {Service} reached its HTTP timeout", name);
            return false;
        }
    }

    private bool HasExpectedIdentity(string name, string body)
    {
        if (name == "scalar")
            return body.Contains($"\"pulseInstance\":\"{InstanceId}\"", StringComparison.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                Matches(root, "instance", InstanceId) &&
                Matches(root, "service", name) &&
                Matches(root, "origin", _publicUrls[name].GetLeftPart(UriPartial.Authority));
        }
        catch (JsonException exception)
        {
            Logger.LogDebug(exception, "Public tunnel probe for {Service} did not return identity JSON", name);
            return false;
        }

        static bool Matches(JsonElement root, string property, string expected) =>
            root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString(), expected, StringComparison.Ordinal);
    }

    public async Task MonitorAsync(CancellationToken cancellationToken)
    {
        var failures = new Dictionary<string, DateTimeOffset>();
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var (name, service) in options.Services)
            {
                if (await ProbeAsync(name, cancellationToken))
                {
                    if (failures.Remove(name))
                        Logger.LogInformation("Public tunnel {Service} recovered", name);
                    continue;
                }

                if (failures.TryAdd(name, DateTimeOffset.UtcNow))
                    Logger.LogWarning("Public tunnel {Service} is unhealthy; attempting recovery", name);
                IsHealthy = false;
                if (DateTimeOffset.UtcNow - failures[name] > TimeSpan.FromSeconds(options.ReconnectTimeoutSeconds))
                    throw new InvalidOperationException($"Public tunnel '{name}' did not recover within DevTunnels:ReconnectTimeoutSeconds. Stopping the enabled run.");

                if (_ownedHosts.TryGetValue(name, out var host) && host.HasExited)
                {
                    await host.DisposeAsync();
                    _ownedHosts.Remove(name);
                    using var recovery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var remaining = TimeSpan.FromSeconds(options.ReconnectTimeoutSeconds) - (DateTimeOffset.UtcNow - failures[name]);
                    if (remaining <= TimeSpan.Zero)
                        throw new InvalidOperationException($"Public tunnel '{name}' exhausted its recovery timeout while stopping the exited host.");
                    recovery.CancelAfter(remaining);
                    try
                    {
                        while (true)
                        {
                            var tunnel = ReadTunnel(Check(await cli.ExecuteAsync(recovery.Token, "show", service.TunnelId, "--json"), "show"));
                            if (tunnel.GetProperty("hostConnections").GetInt32() == 0)
                                break;
                            // The service can briefly retain the exited host's connection metadata.
                            Logger.LogDebug("Waiting for tunnel {TunnelId} to report no active host before restarting", service.TunnelId);
                            await Task.Delay(TimeSpan.FromSeconds(1), recovery.Token);
                        }
                        recovery.Token.ThrowIfCancellationRequested();
                        _ownedHosts.Add(name, cli.StartHost(service.TunnelId));
                    }
                    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new InvalidOperationException(
                            $"Tunnel '{service.TunnelId}' recovery timed out waiting for its previous connection to clear. An active host will not be taken over.", exception);
                    }
                }
            }
            IsHealthy = failures.Count == 0;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Task.WhenAll(_ownedHosts.Values.Select(host => host.DisposeAsync().AsTask()));
        }
        finally
        {
            IsHealthy = false;
            _ownedHosts.Clear();
            foreach (var file in _locks)
                file.Dispose();
            _locks.Clear();
        }
    }
}
