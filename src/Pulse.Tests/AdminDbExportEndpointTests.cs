using System.IO.Compression;
using LiteDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests;

public sealed class AdminDbExportEndpointTests
{
    [Fact]
    public async Task ExportDb_MissingAdminCode_Returns401()
    {
        await using var fixture = await ExportDbTestFixture.CreateAsync();
        using var factory = CreateFactory(fixture.ConnectionString, fixture.AdminCode);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/export-db", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportDb_InvalidAdminCode_Returns403()
    {
        await using var fixture = await ExportDbTestFixture.CreateAsync();
        using var factory = CreateFactory(fixture.ConnectionString, fixture.AdminCode);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/export-db");
        request.Headers.Add(AdminEndpointHandlers.AdminHeaderName, "WRONG-ADMIN-CODE");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExportDb_ValidAdminCode_ReturnsZipWithDbFile()
    {
        await using var fixture = await ExportDbTestFixture.CreateAsync();
        using var factory = CreateFactory(fixture.ConnectionString, fixture.AdminCode);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/export-db");
        request.Headers.Add(AdminEndpointHandlers.AdminHeaderName, fixture.AdminCode);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        Assert.Contains(archive.Entries, e =>
            string.Equals(e.Name, fixture.DatabaseFileName, StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplicationFactory<ApiAssemblyMarker> CreateFactory(string connectionString, string adminCode)
    {
        return new WebApplicationFactory<ApiAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:pulse-db"] = connectionString,
                        ["Security:InstructorCode"] = "TEST-INSTRUCTOR-CODE",
                        ["Security:AdminCode"] = adminCode
                    });
                });
            });
    }

    private sealed class ExportDbTestFixture : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        public string ConnectionString { get; }
        public string AdminCode { get; } = "TEST-ADMIN-CODE";
        public string DatabaseFileName { get; } = "export-test.db";

        private ExportDbTestFixture(string tempDirectory, string connectionString)
        {
            _tempDirectory = tempDirectory;
            ConnectionString = connectionString;
        }

        public static Task<ExportDbTestFixture> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"pulse-export-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            var dbPath = Path.Combine(tempDirectory, "export-test.db");
            var connectionString = $"Filename={dbPath};Connection=shared";

            using (var db = new LiteDatabase(connectionString))
            {
                var sessions = db.GetCollection<Session>("sessions");
                sessions.Insert(new Session
                {
                    Id = Guid.NewGuid(),
                    Title = "Export Test Session",
                    InstructorCode = "TEST-INSTRUCTOR-CODE",
                    JoinCode = "ABC123",
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return Task.FromResult(new ExportDbTestFixture(tempDirectory, connectionString));
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
