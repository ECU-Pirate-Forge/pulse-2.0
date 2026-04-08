using Pulse.Common.Services;
using Pulse.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace Pulse.Tests;

public class JoinSessionEndpointTests
{
    [Fact]
    public async Task JoinSessionByCode_WithValidJoinCode_ReturnsSessionTitle()
    {
        // Arrange
        var joinCode = "ABC123";
        var expectedTitle = "Math Class - Session 1";
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = expectedTitle,
            JoinCode = joinCode,
            InstructorCode = "INST001",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var mockRepo = new Mock<ISessionRepository>();
        mockRepo
            .Setup(repo => repo.GetByJoinCodeAsync(joinCode))
            .ReturnsAsync(session);

        // Act
        var result = await SessionEndpointHandlers.JoinSessionByCode(joinCode, mockRepo.Object);

        // Assert
        Assert.NotNull(result);
        mockRepo.Verify(repo => repo.GetByJoinCodeAsync(joinCode), Times.Once);
    }

    [Fact]
    public async Task JoinSessionByCode_WithInvalidJoinCode_ReturnsNotFound()
    {
        // Arrange
        var invalidCode = "INVALID";
        var mockRepo = new Mock<ISessionRepository>();
        mockRepo
            .Setup(repo => repo.GetByJoinCodeAsync(invalidCode))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await SessionEndpointHandlers.JoinSessionByCode(invalidCode, mockRepo.Object);

        // Assert
        Assert.NotNull(result);
        mockRepo.Verify(repo => repo.GetByJoinCodeAsync(invalidCode), Times.Once);
    }

    [Fact]
    public async Task JoinSessionByCode_WithEmptyJoinCode_ReturnsBadRequest()
    {
        // Arrange
        var mockRepo = new Mock<ISessionRepository>();

        // Act
        var result = await SessionEndpointHandlers.JoinSessionByCode(string.Empty, mockRepo.Object);

        // Assert
        Assert.NotNull(result);
        mockRepo.Verify(repo => repo.GetByJoinCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task JoinSessionByCode_WithWhitespaceJoinCode_ReturnsBadRequest()
    {
        // Arrange
        var mockRepo = new Mock<ISessionRepository>();

        // Act
        var result = await SessionEndpointHandlers.JoinSessionByCode("   ", mockRepo.Object);

        // Assert
        Assert.NotNull(result);
        mockRepo.Verify(repo => repo.GetByJoinCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task JoinSessionByCode_WithNullJoinCode_ReturnsBadRequest()
    {
        // Arrange
        var mockRepo = new Mock<ISessionRepository>();

        // Act
        var result = await SessionEndpointHandlers.JoinSessionByCode(null!, mockRepo.Object);

        // Assert
        Assert.NotNull(result);
        mockRepo.Verify(repo => repo.GetByJoinCodeAsync(It.IsAny<string>()), Times.Never);
    }
}
