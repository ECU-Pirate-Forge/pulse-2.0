using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class SessionJoinLifecycleTests
{
    [Theory]
    [InlineData(SessionStatus.Draft)]
    [InlineData(SessionStatus.Closed)]
    public async Task JoinSession_WhenNotActive_ReturnsConflict(SessionStatus status)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(repo => repo.GetByJoinCodeAsync("ABC123"))
            .ReturnsAsync(new Session { JoinCode = "ABC123", Status = status });

        var result = await SessionEndpointHandlers.JoinSessionByCode("ABC123", repository.Object);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusResult.StatusCode);
    }

    [Fact]
    public async Task JoinSession_WhenActive_ReturnsOk()
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(repo => repo.GetByJoinCodeAsync("ABC123"))
            .ReturnsAsync(new Session { JoinCode = "ABC123", Status = SessionStatus.Active });

        var result = await SessionEndpointHandlers.JoinSessionByCode("ABC123", repository.Object);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
    }
}
