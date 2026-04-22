using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using LiteDB;

namespace Pulse.WebApi;

public static class AdminEndpointHandlers
{
    public const string AdminHeaderName = "AdminCode";

    public static async Task<IResult> ExportDb(
        HttpRequest request,
        IConfiguration configuration,
        LiteDatabase db,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue(AdminHeaderName, out var adminCodeValues)
            || string.IsNullOrWhiteSpace(adminCodeValues.FirstOrDefault()))
        {
            return Results.Unauthorized();
        }

        var configuredAdminCode = configuration["Security:AdminCode"];
        if (string.IsNullOrWhiteSpace(configuredAdminCode))
        {
            return Results.Problem("Server misconfiguration: Security:AdminCode is not set.", statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!IsAdminCodeValid(adminCodeValues.FirstOrDefault(), configuredAdminCode))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var connectionString = configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";
        var dbPath = ResolveDatabasePath(connectionString);

        if (!File.Exists(dbPath))
        {
            return Results.NotFound(new { error = "Database file not found." });
        }

        // Flush pending pages so exported files are as current as possible.
        db.Checkpoint();

        await using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await AddFileToArchiveAsync(archive, dbPath, Path.GetFileName(dbPath), ct);

            var logPath = $"{dbPath}-log";
            if (File.Exists(logPath))
            {
                await AddFileToArchiveAsync(archive, logPath, Path.GetFileName(logPath), ct);
            }
        }

        var fileName = $"pulse-db-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return Results.File(zipStream.ToArray(), "application/zip", fileName);
    }

    private static bool IsAdminCodeValid(string? providedCode, string configuredCode)
    {
        if (string.IsNullOrWhiteSpace(providedCode))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedCode));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredCode));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }

    private static async Task AddFileToArchiveAsync(ZipArchive archive, string sourcePath, string entryName, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920,
            useAsync: true);
        await sourceStream.CopyToAsync(entryStream, ct);
    }

    private static string ResolveDatabasePath(string connectionStringValue)
    {
        var cs = new ConnectionString(connectionStringValue);
        if (string.IsNullOrWhiteSpace(cs.Filename))
        {
            throw new InvalidOperationException("The LiteDB connection string must include Filename.");
        }

        return Path.IsPathRooted(cs.Filename)
            ? cs.Filename
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), cs.Filename));
    }
}
