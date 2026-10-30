using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Aspire.Hosting.ApplicationModel;
using Pulse.AppHost.DevTunnels;
using Scalar.Aspire;
using Pulse.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var publicJoinBaseUrl = builder.Configuration["Pulse:PublicJoinBaseUrl"];
var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"];
var ollamaModel = builder.Configuration["Ollama:Model"];
var ollamaEmbeddingModel = builder.Configuration["Ollama:EmbeddingModel"];
var securityInstructorCode = builder.Configuration["Security:InstructorCode"];
var securityAdminCode = builder.Configuration["Security:AdminCode"];
var pulseDbConnectionString = builder.Configuration.GetConnectionString("pulse-db");

if (!string.IsNullOrWhiteSpace(publicJoinBaseUrl))
    publicJoinBaseUrl = PublicJoinUrl.Normalize(publicJoinBaseUrl);

    var options = builder.Configuration.GetSection("DevTunnels").Get<DevTunnelOptions>() ?? new();
    
    if(options.Enabled)
    {
        options.Validate();
    }

    if (options.Enabled && (builder.ExecutionContext.IsPublishMode || !builder.Environment.IsDevelopment()))
        throw new InvalidOperationException("DevTunnels can only be enabled for local Development runs, not publishing or production.");

    using var logging = LoggerFactory.Create(log => log.AddSimpleConsole());
    var logger = logging.CreateLogger("Pulse.DevTunnels");
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
    await using var tunnels = new DevTunnelManager(options, new DevTunnelCli(options.CliPath, logger), http, logger);
    if (options.Enabled)
    {
        await tunnels.PrepareAsync(CancellationToken.None);
    }

    builder.AddProject<Projects.Pulse_MigrationManager>("migration");

    var webapi = builder.AddProject<Projects.Pulse_WebApi>("webapi")
        .WithEnvironment("ConnectionStrings__pulse-db", pulseDbConnectionString)
        .WithEnvironment("Security__InstructorCode", securityInstructorCode)
        .WithEnvironment("Security__AdminCode", securityAdminCode)
        .WithEnvironment("Ollama__BaseUrl", ollamaBaseUrl)
        .WithEnvironment("Ollama__Model", ollamaModel)
        .WithEnvironment("Ollama__EmbeddingModel", ollamaEmbeddingModel)
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health");

    var webadmin = builder.AddProject<Projects.Pulse_Admin>("webadmin")
        .WaitFor(webapi)
        .WithReference(webapi)
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health");

    var webapp = builder.AddProject<Projects.Pulse_WebApp>("webapp")
        .WaitFor(webapi)
        .WithReference(webapi)
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health");

    var scalar = builder.AddScalarApiReference("scalar");
    scalar.WithApiReference(webapi)
        .WithExternalHttpEndpoints()
        .WaitFor(webapi);

    if (options.Enabled)
    {
        DevTunnelWiring.ConfigureApplication(webapi, options, tunnels);
        DevTunnelWiring.ConfigureApplication(webadmin, options, tunnels);
        DevTunnelWiring.ConfigureApplication(webapp, options, tunnels);
        builder.Services.AddHealthChecks().AddCheck("pulse-dev-tunnels", () =>
            tunnels.IsHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Public tunnels are not verified or connectivity is recovering."));
        builder.AddResource(new DevTunnelWiring.DevTunnelStatusResource("dev-tunnels"))
            .WithInitialState(new() { ResourceType = "DevTunnels", State = KnownResourceStates.Running, Properties = [] })
            .WithHealthCheck("pulse-dev-tunnels");
    }

    await using var app = builder.Build();
    if (!options.Enabled)
    {
        await app.RunAsync();
        return;
    }

    logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Pulse.DevTunnels");
    tunnels.Logger = logger;
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    using var monitoring = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
    Task? monitor = null;
    try
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(monitoring.Token);
        startup.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));
        logger.LogInformation("Starting Aspire resources for tunnel mode.");
        await app.StartAsync(startup.Token).WaitAsync(startup.Token);
        logger.LogInformation("Aspire started; verifying public tunnel routing.");
        await tunnels.WaitForReadyAsync(monitoring.Token);
        monitor = tunnels.MonitorAsync(monitoring.Token);
        var shutdown = app.WaitForShutdownAsync(monitoring.Token);
        var finished = await Task.WhenAny(shutdown, monitor);
        await finished;
    }
    catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
    {
        logger.LogInformation("AppHost is stopping; shutting down owned tunnel hosts.");
    }
    catch (OperationCanceledException exception) when (!lifetime.ApplicationStopping.IsCancellationRequested)
    {
        logger.LogError(exception, "Aspire resource startup exceeded the configured timeout.");
        throw new TimeoutException("Aspire startup timed out. Check AppHost resource logs and that the Aspire CLI matches the project's 13.1 release family.", exception);
    }
    catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
    {
        logger.LogError(exception, "Dev tunnel mode failed; stopping this AppHost: {Reason}", exception.Message);
        throw;
    }
    finally
    {
        await monitoring.CancelAsync();
        try
        {
            if (monitor is not null)
                await monitor;
        }
        catch (OperationCanceledException) when (monitoring.IsCancellationRequested)
        {
            // Cancellation is expected when AppHost stops.
        }
        finally
        {
            await app.StopAsync();
        }
    }