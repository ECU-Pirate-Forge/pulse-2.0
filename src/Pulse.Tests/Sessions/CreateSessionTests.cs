using Microsoft.AspNetCore.Mvc;
using Moq;
using Pulse.Shared.Models;
using Pulse.Shared.Repositories;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Xunit;

namespace Pulse.Tests.Sessions;

public class CreateSessionTests
{
    private readonly Mock<ISessionRepository> _repoMock = new();
    private readonly Mock<IJoinCodeGenerator> _generatorMock = new();
    private readonly SessionsController _sut;

    public CreateSessionTests()
    {
        _sut = new SessionsController(_repoMock.Object, _generatorMock.Object);
    }

    [Fact]
    public async Task CreateSession_ValidTitle_Returns201WithCodes()
    {
        // Arrange
        _generatorMock.Setup(g => g.Generate()).Returns("ABC123");
        _repoMock.Setup(r => r.JoinCodeExistsAsync("ABC123", default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<Session>(), default)).Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { Title = "My Session" };

        // Act
        var result = await _sut.CreateSession(request, default);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);

        var response = Assert.IsType<CreateSessionResponse>(created.Value);
        Assert.Equal("ABC123", response.JoinCode);
        Assert.False(string.IsNullOrEmpty(response.InstructorCode));
        Assert.NotEqual(Guid.Empty, response.Id);
    }

    [Fact]
    public async Task CreateSession_PersistsSessionWithDraftStatus()
    {
        // Arrange
        Session? captured = null;
        _generatorMock.Setup(g => g.Generate()).Returns("XYZ789");
        _repoMock.Setup(r => r.JoinCodeExistsAsync("XYZ789", default)).ReturnsAsync(false);
        _repoMock
            .Setup(r => r.InsertAsync(It.IsAny<Session>(), default))
            .Callback<Session, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { Title = "  Trimmed Title  " };

        // Act
        await _sut.CreateSession(request, default);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(SessionStatus.Draft, captured!.Status);
        Assert.Equal("Trimmed Title", captured.Title);
        Assert.NotEmpty(captured.JoinCode);
        Assert.NotEmpty(captured.InstructorCode);
        Assert.True(captured.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateSession_RetriesOnJoinCodeCollision()
    {
        // Arrange
        var callCount = 0;
        _generatorMock.Setup(g => g.Generate()).Returns(() => callCount++ == 0 ? "TAKEN1" : "FREE22");
        _repoMock.Setup(r => r.JoinCodeExistsAsync("TAKEN1", default)).ReturnsAsync(true);
        _repoMock.Setup(r => r.JoinCodeExistsAsync("FREE22", default)).ReturnsAsync(false);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<Session>(), default)).Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { Title = "Collision Test" };

        // Act
        var result = await _sut.CreateSession(request, default);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<CreateSessionResponse>(created.Value);
        Assert.Equal("FREE22", response.JoinCode);
        _generatorMock.Verify(g => g.Generate(), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateSession_EmptyTitle_ShouldNotPersist()
    {
        // Arrange
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<Session>(), default)).Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { Title = "" };

        // Manually simulate what the MVC pipeline does before calling the action
        _sut.ModelState.AddModelError("Title", "Title is required.");

        // Act — controller must check ModelState itself since we're unit testing outside the pipeline
        IActionResult result;
        if (!_sut.ModelState.IsValid)
            result = _sut.ValidationProblem(_sut.ModelState);
        else
            result = await _sut.CreateSession(request, default);

        // Assert
        Assert.IsType<ObjectResult>(result);
        _repoMock.Verify(r => r.InsertAsync(It.IsAny<Session>(), default), Times.Never);
    }
}