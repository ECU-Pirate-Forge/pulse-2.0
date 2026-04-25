using LiteDB;
using Pulse.Shared.Models;
using Pulse.Common.Services;
using Pulse.Shared.Services;

namespace Pulse.Tests.Tests;

public class SessionRepositoryTests
{
    private SessionRepository CreateRepository()
    {
        var db = new LiteDatabase("Filename=:memory:");
        return new SessionRepository(db, new JoinCodeGenerator());
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WhenSessionExists_ReturnsSession()
    {
        // Arrange
        var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            JoinCode = "ABC123",
            InstructorCode = "INST001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        collection.Insert(session);

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        // Act
        var result = await repo.GetByJoinCodeAsync("ABC123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ABC123", result.JoinCode);
        Assert.Equal(session.Id, result.Id);
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WhenSessionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByJoinCodeAsync("NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByJoinCodeAsync_WithNullOrEmpty_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act & Assert
        Assert.Null(await repo.GetByJoinCodeAsync(null!));
        Assert.Null(await repo.GetByJoinCodeAsync(""));
        Assert.Null(await repo.GetByJoinCodeAsync("   "));
    }

    [Fact]
    public async Task GenerateUniqueJoinCodeAsync_ReturnsValidCode()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var code = await repo.GenerateUniqueJoinCodeAsync();

        // Assert
        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(code.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public async Task GenerateUniqueJoinCodeAsync_ChecksForCollision()
    {
        // Arrange
        var db = new LiteDatabase("Filename=:memory:");
        var collection = db.GetCollection<Session>("sessions");

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            JoinCode = "DUPLICATE",
            InstructorCode = "INST001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        collection.Insert(session);

        var repo = new SessionRepository(db, new JoinCodeGenerator());

        // Act
        var code1 = await repo.GenerateUniqueJoinCodeAsync();
        var code2 = await repo.GenerateUniqueJoinCodeAsync();

        // Assert
        Assert.NotEqual(code1, code2);
        Assert.NotEqual("DUPLICATE", code1);
        Assert.NotEqual("DUPLICATE", code2);
    }
}
