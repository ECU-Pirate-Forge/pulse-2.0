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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
            // OpenTelemetry: wraps migration work in a trace span visible in the Aspire dashboard
            using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);

            try
            {
                logger.LogInformation("Starting migrations...");
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
                logger.LogInformation("Finished migrations...");
                // Stop the worker process — tells Aspire this service has completed
                hostApplicationLifetime.StopApplication();
            }
    }

    private static async Task RunMigrationsAsync(ILogger logger, CancellationToken cancellationToken)
    {
        // Placeholder for migration logic.
        logger.LogInformation("Starting migrations...");

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

        logger.LogInformation("Migrations completed.");
    }
}
