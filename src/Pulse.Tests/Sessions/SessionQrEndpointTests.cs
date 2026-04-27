using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;

namespace Pulse.Tests.Sessions;

public class SessionQrEndpointTests
{
    [Fact]
    public async Task GetSessionQrReturnsPngWhenSessionExists()
    {
        using var db = new LiteDatabase("Filename=:memory:");
        var repo = new SessionRepository(db, new JoinCodeGenerator());
        var collection = db.GetCollection<Session>("sessions");

        var sessionId = Guid.NewGuid();
        collection.Insert(new Session
        {
            Id = sessionId,
            Title = "QR Session",
            InstructorCode = "INST001",
            JoinCode = "JOIN42",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("pulse.example");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:JoinBaseUrl"] = "https://pulse.example"
            })
            .Build();

        var result = await SessionEndpointHandlers.GetSessionQr(sessionId, context.Request, repo, configuration);
        var fileResult = Assert.IsType<FileContentHttpResult>(result);
        var fileBytes = fileResult.FileContents.ToArray();

        Assert.Equal("image/png", fileResult.ContentType);
        Assert.True(fileBytes.Length > 8);
        Assert.Equal(0x89, fileBytes[0]);
        Assert.Equal(0x50, fileBytes[1]);
        Assert.Equal(0x4E, fileBytes[2]);
        Assert.Equal(0x47, fileBytes[3]);
    }

    [Fact]
    public async Task GetSessionQrReturnsNotFoundWhenSessionDoesNotExist()
    {
        using var db = new LiteDatabase("Filename=:memory:");
        var repo = new SessionRepository(db, new JoinCodeGenerator());

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("pulse.example");

        var configuration = new ConfigurationBuilder().Build();

        var result = await SessionEndpointHandlers.GetSessionQr(Guid.NewGuid(), context.Request, repo, configuration);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusResult.StatusCode);
    }
}
