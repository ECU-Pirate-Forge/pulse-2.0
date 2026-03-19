using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace Pulse.MigrationManager;

public class Worker(ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);
    private static readonly Action<ILogger, Exception?> s_startingMigrations =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "StartingMigrations"), "Starting migrations...");

    private static readonly Action<ILogger, Exception?> s_finishedMigrations =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "FinishedMigrations"), "Finished migrations...");

    private static readonly Action<ILogger, Exception?> s_migrationsCompleted =
        LoggerMessage.Define(LogLevel.Information, new EventId(3, "MigrationsCompleted"), "Migrations completed.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // OpenTelemetry: wraps migration work in a trace span visible in the Aspire dashboard
        using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            s_startingMigrations(logger, null);
            using var scope = serviceProvider.CreateScope();
            // var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // await RunMigrationAsync(dbContext, stoppingToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            s_finishedMigrations(logger, null);
            // Stop the worker process — tells Aspire this service has completed
            hostApplicationLifetime.StopApplication();
        }
    }

    private static async Task RunMigrationsAsync(ILogger logger, CancellationToken cancellationToken)
    {
        // Placeholder for migration logic.
        s_startingMigrations(logger, null);

        // // CreateExecutionStrategy handles transient SQL Server errors (e.g., the
        // // container isn't fully ready yet) with automatic retries.
        // var strategy = dbContext.Database.CreateExecutionStrategy();

        // await strategy.ExecuteAsync(async () =>
        // {
        //     // MigrateAsync applies all pending migrations in order.
        //     // On a fresh database, this creates all tables and inserts seed data.
        //     // On subsequent runs, it applies only new migrations (idempotent).
        //     await dbContext.Database.MigrateAsync(cancellationToken);
        // });

        s_migrationsCompleted(logger, null);
    }
}
