using Moq;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Sessions;

public class JoinCodeGeneratorTests
{
    private readonly JoinCodeGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsExactly6Characters()
    {
        var code = _sut.Generate();
        Assert.Equal(6, code.Length);
    }

    [Fact]
    public void Generate_ReturnsOnlyUpperAlphanumeric()
    {
        for (int i = 0; i < 100; i++)
        {
            var code = _sut.Generate();
            Assert.Matches("^[A-Z0-9]{6}$", code);
        }
    }

    [Fact]
    public void Generate_ProducesDifferentCodesOverTime()
    {
        var codes = Enumerable.Range(0, 20).Select(_ => _sut.Generate()).ToHashSet();
        Assert.True(codes.Count > 1, "Generator should not produce the same code every time.");
    }

    [Fact]
    public async Task CollisionRetry_RegeneratesOnDuplicate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var repoMock = new Mock<ISessionRepository>();
        var generatorMock = new Mock<IJoinCodeGenerator>();

        var callCount = 0;
        generatorMock.Setup(g => g.Generate()).Returns(() => callCount++ == 0 ? "TAKEN1" : "FREE22");
        repoMock.Setup(r => r.JoinCodeExistsAsync("TAKEN1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repoMock.Setup(r => r.JoinCodeExistsAsync("FREE22", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        string joinCode;
        do
        {
            joinCode = generatorMock.Object.Generate();
        }
        while (await repoMock.Object.JoinCodeExistsAsync(joinCode, cancellationToken));

        Assert.Equal("FREE22", joinCode);
        generatorMock.Verify(g => g.Generate(), Times.Exactly(2));
    }
}

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
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        collection.Insert(new Session
        {
            Id = Guid.NewGuid(),
            Title = "Session B",
            InstructorCode = "OTHER",
            JoinCode = "ZZZ999",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var repo = new SessionRepository(db);

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
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var repo = new SessionRepository(db);

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