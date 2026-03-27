using LiteDB;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Xunit;

namespace Pulse.Tests;

public class SessionRepositoryTests
{
    private LiteDatabase CreateInMemoryDatabase()
    {
        return new LiteDatabase(":memory:");
    }

    [Fact]
    public void GenerateJoinCode_ReturnsValidCode()
    {
        // Arrange
        using var db = CreateInMemoryDatabase();
        var repository = new SessionRepository(db);

        // Act
        var code = repository.GenerateJoinCode();

        // Assert
        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(code.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void GenerateJoinCode_GeneratesUniqueCodes()
    {
        // Arrange
        using var db = CreateInMemoryDatabase();
        var repository = new SessionRepository(db);
        var sessions = db.GetCollection<Session>("sessions");

        // Add a test session
        var testSession = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test Session",
            JoinCode = "ABC123",
            InstructorCode = "INST001",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        sessions.Insert(testSession);

        // Act
        var newCode = repository.GenerateJoinCode();

        // Assert
        Assert.NotEqual("ABC123", newCode);
        Assert.Equal(6, newCode.Length);
    }

    [Fact]
    public void GetByJoinCode_ReturnsSessionWhenExists()
    {
        // Arrange
        using var db = CreateInMemoryDatabase();
        var repository = new SessionRepository(db);
        var sessions = db.GetCollection<Session>("sessions");

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Test Session",
            JoinCode = "XYZ789",
            InstructorCode = "INST001",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        sessions.Insert(session);

        // Act
        var result = repository.GetByJoinCode("XYZ789");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("XYZ789", result.JoinCode);
        Assert.Equal("Test Session", result.Title);
    }

    [Fact]
    public void GetByJoinCode_ReturnsNullWhenNotExists()
    {
        // Arrange
        using var db = CreateInMemoryDatabase();
        var repository = new SessionRepository(db);

        // Act
        var result = repository.GetByJoinCode("NOTEXIST");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetByJoinCode_ReturnsNullForNullOrWhitespace(string joinCode)
    {
        // Arrange
        using var db = CreateInMemoryDatabase();
        var repository = new SessionRepository(db);

        // Act
        var result = repository.GetByJoinCode(joinCode);

        // Assert
        Assert.Null(result);
    }
}
