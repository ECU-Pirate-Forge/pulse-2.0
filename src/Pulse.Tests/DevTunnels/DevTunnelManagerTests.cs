using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pulse.AppHost.DevTunnels;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelManagerTests
{
    [Fact]
    public async Task DisabledModeNeverInvokesCli()
    {
        var cli = new FakeCli();
        using var http = new HttpClient();
        await using var manager = Manager(new(), cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Empty(cli.Commands);
        Assert.Empty(cli.Hosts);
    }

    [Fact]
    public async Task MissingTunnelsAreCreatedAnonymousAndOwnedHostsAreStoppedWithoutDeletingDefinitions()
    {
        var options = Options();
        var cli = new FakeCli();
        using var http = new HttpClient();
        await using (var manager = Manager(options, cli, http))
        {
            await manager.PrepareAsync(TestContext.Current.CancellationToken);
            Assert.Equal(4, manager.PublicUrls.Count);
            Assert.Equal(4, cli.Hosts.Count);
            Assert.All(manager.PublicUrls.Values, uri => Assert.Equal("https", uri.Scheme));
            Assert.All(cli.Hosts, host => Assert.False(host.Disposed));
        }
        Assert.All(cli.Hosts, host => Assert.True(host.Disposed));
        Assert.Equal(4, cli.Definitions.Count);
        Assert.DoesNotContain(cli.Commands, command => command.StartsWith("delete"));
        Assert.Equal(4, cli.Commands.Count(command => command.StartsWith("create ")));
    }

    [Fact]
    public async Task ExistingStoppedDefinitionsKeepTheirUrlsAcrossRestarts()
    {
        var options = Options();
        var cli = new FakeCli();
        using var http = new HttpClient();
        Dictionary<string, Uri> first;
        await using (var manager = Manager(options, cli, http))
        {
            await manager.PrepareAsync(TestContext.Current.CancellationToken);
            first = manager.PublicUrls.ToDictionary();
        }
        await using (var manager = Manager(options, cli, http))
        {
            await manager.PrepareAsync(TestContext.Current.CancellationToken);
            Assert.Equal(first.OrderBy(p => p.Key), manager.PublicUrls.OrderBy(p => p.Key));
        }
        Assert.Equal(4, cli.Commands.Count(command => command.StartsWith("create ")));
    }

    [Fact]
    public async Task ExistingRunningHostsAreReusedWithoutStartingOrStoppingThem()
    {
        var options = Options();
        var cli = new FakeCli();
        cli.Seed(options, running: true);
        using var http = new HttpClient();
        await using (var manager = Manager(options, cli, http))
        {
            await manager.PrepareAsync(TestContext.Current.CancellationToken);
            Assert.Equal(4, manager.PublicUrls.Count);
            Assert.Empty(cli.Hosts);
        }
        Assert.All(cli.Definitions.Values, definition => Assert.Equal(1, definition.HostConnections));
        Assert.DoesNotContain(cli.Commands, command => command.StartsWith("create ") || command.StartsWith("access create "));
    }

    [Fact]
    public async Task ActiveIncompatibleMappingFailsWithoutRepointingOrStartingHost()
    {
        var options = Options();
        var cli = new FakeCli();
        cli.Seed(options, running: true);
        cli.Definitions[options.Services["webapi"].TunnelId].Port++;
        using var http = new HttpClient();
        await using var manager = Manager(options, cli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("unrelated ports", exception.Message);
        Assert.Empty(cli.Hosts);
        Assert.DoesNotContain(cli.Commands, command => command.StartsWith("port create "));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DenyRulesAreNeverRemoved(bool running)
    {
        var options = Options();
        var cli = new FakeCli { Deny = true };
        cli.Seed(options, running);
        using var http = new HttpClient();
        await using var manager = Manager(options, cli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("deny rules", exception.Message);
        Assert.DoesNotContain(cli.Commands, command => command.StartsWith("access create ") || command.StartsWith("access reset "));
    }

    [Fact]
    public async Task AuthenticationFailureIsNotMistakenForMissingTunnel()
    {
        var options = Options();
        var cli = new FakeCli { ShowFailure = new(3, "", "Access denied") };
        using var http = new HttpClient();
        await using var manager = Manager(options, cli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Access denied", exception.Message);
        Assert.DoesNotContain(cli.Commands, command => command.StartsWith("create "));
    }

    [Fact]
    public async Task MissingLoginFailsWithSetupInstructions()
    {
        var cli = new FakeCli { LoggedIn = false };
        using var http = new HttpClient();
        await using var manager = Manager(Options(), cli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("devtunnel user login", exception.Message);
        Assert.Empty(cli.Hosts);
    }

    [Fact]
    public async Task MalformedJsonFailsExplicitly()
    {
        var cli = new FakeCli { ShowFailure = new(0, "not json", "") };
        using var http = new HttpClient();
        await using var manager = Manager(Options(), cli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("malformed JSON", exception.Message);
    }

    [Fact]
    public async Task NewTunnelWithoutPortsPropertyGetsItsConfiguredPort()
    {
        var options = Options();
        var cli = new FakeCli { OmitEmptyPorts = true };
        using var http = new HttpClient();
        await using var manager = Manager(options, cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, cli.Commands.Count(command => command.StartsWith("port create ")));
        Assert.Equal(4, manager.PublicUrls.Count);
    }

    [Fact]
    public async Task PartialStartupFailureCleansUpOwnedHostsAndReleasesLocks()
    {
        var options = Options();
        var cli = new FakeCli { FailOnSecondService = true };
        using var http = new HttpClient();
        await using (var manager = Manager(options, cli, http))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
        }
        Assert.Single(cli.Hosts);
        Assert.True(cli.Hosts[0].Disposed);
        cli.FailOnSecondService = false;
        await using var retry = Manager(options, cli, http);
        await retry.PrepareAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReadinessRequiresThisLaunchIdentityOnAllPublicServices()
    {
        var cli = new FakeCli();
        var probe = new ProbeHandler();
        using var http = new HttpClient(probe);
        await using var manager = Manager(Options(), cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        probe.ResponseBody = uri => JsonSerializer.Serialize(new
        {
            instance = manager.InstanceId,
            pulseInstance = manager.InstanceId,
            service = manager.PublicUrls.Single(pair => pair.Value.Host == uri.Host).Key,
            origin = uri.GetLeftPart(UriPartial.Authority)
        });
        await manager.WaitForReadyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, probe.Requests.Count);
        Assert.All(probe.Requests, request => Assert.Equal("https", request.Scheme));
        Assert.True(probe.SkippedInterstitial);
    }

    [Fact]
    public async Task RemoteOrWrongHostDoesNotPassReadiness()
    {
        var options = Options();
        options.StartupTimeoutSeconds = 1;
        var cli = new FakeCli();
        cli.Seed(options, running: true);
        using var http = new HttpClient(new ProbeHandler { Body = "another-apphost" });
        await using var manager = Manager(options, cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(() => manager.WaitForReadyAsync(TestContext.Current.CancellationToken));
        Assert.Empty(cli.Hosts);
    }

    [Fact]
    public async Task SustainedDisconnectFailsInsteadOfFallingBackToLocalhost()
    {
        var options = Options();
        options.ReconnectTimeoutSeconds = 1;
        var cli = new FakeCli();
        using var http = new HttpClient(new ProbeHandler { Body = "offline" });
        await using var manager = Manager(options, cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.MonitorAsync(TestContext.Current.CancellationToken));
        Assert.Contains("did not recover", exception.Message);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("service")]
    [InlineData("origin")]
    public async Task ReadinessRejectsMismatchedFieldsEvenWhenBodyContainsExpectedValues(string wrongField)
    {
        var options = Options();
        options.StartupTimeoutSeconds = 1;
        var cli = new FakeCli();
        var probe = new ProbeHandler();
        using var http = new HttpClient(probe);
        await using var manager = Manager(options, cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        probe.ResponseBody = uri => JsonSerializer.Serialize(new
        {
            instance = wrongField == "instance" ? "another-launch" : manager.InstanceId,
            service = wrongField == "service" ? "another-service" : manager.PublicUrls.Single(pair => pair.Value.Host == uri.Host).Key,
            origin = wrongField == "origin" ? "http://localhost" : uri.GetLeftPart(UriPartial.Authority),
            unrelatedText = manager.InstanceId + string.Join(" ", manager.PublicUrls.Values)
        });
        await Assert.ThrowsAsync<TimeoutException>(() => manager.WaitForReadyAsync(TestContext.Current.CancellationToken));
        Assert.False(manager.IsHealthy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingAnonymousAccessIsAddedOnlyToStoppedTunnels(bool running)
    {
        var options = Options();
        var cli = new FakeCli();
        cli.Seed(options, running);
        foreach (var definition in cli.Definitions.Values)
            definition.Anonymous = false;
        using var http = new HttpClient();
        await using var manager = Manager(options, cli, http);
        if (running)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PrepareAsync(TestContext.Current.CancellationToken));
            Assert.Contains("lacks persistent anonymous", exception.Message);
            Assert.Empty(cli.Hosts);
            Assert.DoesNotContain(cli.Commands, command => command.StartsWith("access create "));
        }
        else
        {
            await manager.PrepareAsync(TestContext.Current.CancellationToken);
            Assert.Equal(4, cli.Commands.Count(command => command.StartsWith("access create ")));
            Assert.All(cli.Definitions.Values, definition => Assert.True(definition.Anonymous));
        }
    }

    [Fact]
    public async Task ConcurrentAppHostCannotAcquireAnAlreadyOwnedTunnel()
    {
        var options = Options();
        using var http = new HttpClient();
        await using var owner = Manager(options, new FakeCli(), http);
        await owner.PrepareAsync(TestContext.Current.CancellationToken);
        var contenderCli = new FakeCli();
        await using var contender = Manager(options, contenderCli, http);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => contender.PrepareAsync(TestContext.Current.CancellationToken));
        Assert.Contains("already managed", exception.Message);
        Assert.Empty(contenderCli.Hosts);
    }

    [Fact]
    public async Task ProvisioningHonorsCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var http = new HttpClient();
        await using var manager = Manager(Options(), new FakeCli(), http);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.PrepareAsync(cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedCliIsBoundedByStartupTimeoutOrCallerCancellation(bool callerCancels)
    {
        var options = Options();
        options.StartupTimeoutSeconds = 1;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (callerCancels)
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        using var http = new HttpClient();
        await using var manager = Manager(options, new BlockingCli(), http);
        if (callerCancels)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.PrepareAsync(cancellation.Token));
        else
        {
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => manager.PrepareAsync(cancellation.Token));
            Assert.Contains("provisioning timed out", exception.Message);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ExitedOwnedHostIsRestartedAndItsPublicUrlIsPreserved(int staleHostReads)
    {
        var cli = new FakeCli();
        var probe = new ProbeHandler();
        using var http = new HttpClient(probe);
        await using var manager = Manager(Options(), cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        var originalUrls = manager.PublicUrls.ToDictionary();
        cli.Hosts[0].HasExited = true;
        cli.StaleHostReads = staleHostReads;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        probe.ResponseBody = _ =>
        {
            if (cli.Hosts.Count == 5)
                cancellation.Cancel();
            return "offline";
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.MonitorAsync(cancellation.Token));
        Assert.Equal(5, cli.Hosts.Count);
        Assert.True(cli.Hosts[0].Disposed);
        Assert.Equal(originalUrls.OrderBy(pair => pair.Key), manager.PublicUrls.OrderBy(pair => pair.Key));
    }

    [Fact]
    public async Task RecoveryRefusesToTakeOverAnotherActiveHost()
    {
        var options = Options();
        options.ReconnectTimeoutSeconds = 1;
        var cli = new FakeCli();
        using var http = new HttpClient(new ProbeHandler { Body = "offline" });
        await using var manager = Manager(options, cli, http);
        await manager.PrepareAsync(TestContext.Current.CancellationToken);
        cli.Hosts[0].HasExited = true;
        cli.ShowFailure = new(0, """{"tunnel":{"hostConnections":1}}""", "");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.MonitorAsync(TestContext.Current.CancellationToken));
        Assert.Contains("active host", exception.Message);
        Assert.Equal(4, cli.Hosts.Count);
    }

    [Fact]
    public async Task MissingCliProducesActionableError()
    {
        var cli = new DevTunnelCli(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), NullLogger.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cli.ExecuteAsync(TestContext.Current.CancellationToken, "user", "show", "--json"));
        Assert.Contains("Install the Microsoft devtunnel CLI", exception.Message);
    }

    private static DevTunnelOptions Options()
    {
        var options = DevTunnelOptionsTests.CreateOptions();
        var prefix = Guid.NewGuid().ToString("N");
        foreach (var (name, service) in options.Services)
            service.TunnelId = $"test-{prefix}-{name}";
        return options;
    }

    private static DevTunnelManager Manager(DevTunnelOptions options, IDevTunnelCli cli, HttpClient http) =>
        new(options, cli, http, NullLogger.Instance);

    internal sealed class ProbeHandler : HttpMessageHandler
    {
        public string Body { get; set; } = "";
        public Func<Uri, string>? ResponseBody { get; set; }
        public List<Uri> Requests { get; } = [];
        public bool SkippedInterstitial { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            SkippedInterstitial = request.Headers.Contains("X-Tunnel-Skip-AntiPhishing-Page");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody?.Invoke(request.RequestUri!) ?? Body)
            });
        }
    }

    internal sealed class FakeHost(Action onDispose) : IDevTunnelHost
    {
        public bool HasExited { get; set; }
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            onDispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingCli : IDevTunnelCli
    {
        public async Task<DevTunnelCommandResult> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocked CLI unexpectedly completed.");
        }

        public IDevTunnelHost StartHost(string tunnelId) =>
            throw new InvalidOperationException("The blocked CLI must not start a host.");
    }

    internal sealed class Definition
    {
        public int Port { get; set; }
        public int HostConnections { get; set; }
        public bool Anonymous { get; set; } = true;
    }

    internal sealed class FakeCli : IDevTunnelCli
    {
        public List<string> Commands { get; } = [];
        public Dictionary<string, Definition> Definitions { get; } = [];
        public List<FakeHost> Hosts { get; } = [];
        public DevTunnelCommandResult? ShowFailure { get; set; }
        public bool LoggedIn { get; set; } = true;
        public bool FailOnSecondService { get; set; }
        public bool Deny { get; set; }
        public bool OmitEmptyPorts { get; set; }
        public int StaleHostReads { get; set; }

        public void Seed(DevTunnelOptions options, bool running)
        {
            foreach (var service in options.Services.Values)
                Definitions.Add(service.TunnelId, new() { Port = service.Port, HostConnections = running ? 1 : 0 });
        }

        public Task<DevTunnelCommandResult> ExecuteAsync(CancellationToken cancellationToken, params string[] args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(string.Join(" ", args));
            DevTunnelCommandResult Json(object value) => new(0, JsonSerializer.Serialize(value), "");
            if (args[0] == "user")
                return Task.FromResult(Json(new { username = LoggedIn ? "test" : "" }));
            if (args[0] == "show")
            {
                if (ShowFailure is not null)
                    return Task.FromResult(ShowFailure);
                if (FailOnSecondService && args[1].EndsWith("webadmin"))
                    return Task.FromResult(new DevTunnelCommandResult(3, "", "Provisioning denied"));
                if (!Definitions.TryGetValue(args[1], out var definition))
                    return Task.FromResult(new DevTunnelCommandResult(2, "", "Not found"));
                if (OmitEmptyPorts && definition.Port == 0)
                    return Task.FromResult(Json(new { tunnel = new { tunnelId = args[1] + ".use2", hostConnections = 0 } }));
                var hostConnections = definition.HostConnections;
                if (StaleHostReads > 0)
                {
                    StaleHostReads--;
                    hostConnections = 1;
                }
                return Task.FromResult(Json(new
                {
                    tunnel = new
                    {
                        tunnelId = args[1] + ".use2",
                        hostConnections,
                        ports = definition.Port == 0 ? [] : new[]
                        {
                            new { portNumber = definition.Port, protocol = "http",
                                portUri = $"https://{args[1]}-{definition.Port}.use2.devtunnels.ms/" }
                        }
                    }
                }));
            }
            if (args[0] == "create")
            {
                Definitions.Add(args[1], new());
                return Task.FromResult(Json(new { }));
            }
            if (args[0] == "port" && args[1] == "create")
            {
                Definitions[args[2]].Port = int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
                return Task.FromResult(Json(new { }));
            }
            if (args[0] == "port" && args[1] == "update")
                return Task.FromResult(Json(new { }));
            if (args[0] == "access" && args[1] == "list")
                return Task.FromResult(Json(new
                {
                    accessControlEntries = !Definitions[args[2]].Anonymous ? [] : new[]
                    {
                        new { type = "Anonymous", scopes = new[] { "connect" }, isDeny = Deny }
                    }
                }));
            if (args[0] == "access" && args[1] == "create")
            {
                Definitions[args[2]].Anonymous = true;
                return Task.FromResult(Json(new { }));
            }
            throw new InvalidOperationException("Unexpected fake CLI command: " + string.Join(" ", args));
        }

        public IDevTunnelHost StartHost(string tunnelId)
        {
            Definitions[tunnelId].HostConnections = 1;
            var host = new FakeHost(() => Definitions[tunnelId].HostConnections = 0);
            Hosts.Add(host);
            return host;
        }
    }
}
