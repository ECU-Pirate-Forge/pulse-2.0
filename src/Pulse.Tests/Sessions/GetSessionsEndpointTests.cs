using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Sessions;

public class GetSessionsByInstructorCodeEndpointTests
{
    [Fact]
    public async Task GetSessionsValidInstructorCodeReturns200AndArray()
    {
        using var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");
        collection.Insert(new Session
        {
            Id = Guid.NewGuid(),
            Title = "Session A",
            InstructorCode = "INST001",
            JoinCode = "ABC123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        collection.Insert(new Session
        {
            Id = Guid.NewGuid(),
            Title = "Session B",
            InstructorCode = "OTHER",
            JoinCode = "ZZZ999",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST001";

        var result = await SessionEndpointHandlers.GetSessions(context, repo);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

        var sessions = Assert.IsAssignableFrom<IEnumerable<Session>>(valueResult.Value);
        var list = sessions.ToList();

        Assert.Single(list);
        Assert.Equal("INST001", list[0].InstructorCode);
    }

    [Fact]
    public async Task GetSessionsNoMatchingCodeReturnsEmptyArray()
    {
        using var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");
        collection.Insert(new Session
        {
            Id = Guid.NewGuid(),
            Title = "Session A",
            InstructorCode = "OTHER",
            JoinCode = "ABC123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = "INST001";

        var result = await SessionEndpointHandlers.GetSessions(context, repo);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);

        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

        var sessions = Assert.IsAssignableFrom<IEnumerable<Session>>(valueResult.Value);
        Assert.Empty(sessions);
    }
}
