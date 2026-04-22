using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class UnblindSessionEndpointTests
{
    private Mock<ISessionRepository> BuildRepo(Session? session = null)
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(session);
        repo.Setup(r => r.Update(It.IsAny<Session>()))
            .Returns(true);
        return repo;
    }

    private HttpContext BuildContext(string? instructorCode = null)
    {
        var context = new DefaultHttpContext();
        if (instructorCode is not null)
            context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }

    [Fact]
    public async Task UnblindSession_ValidRequest_Returns200AndSetsIsUnblinded()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Title = "Test", InstructorCode = "INST001" };
        var repo = BuildRepo(session);
        var context = BuildContext("INST001");

        var result = await SessionEndpointHandlers.UnblindSession(sessionId, context, repo.Object);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<Session>>(result);
        Assert.True(ok.Value!.IsUnblinded);
        repo.Verify(r => r.Update(It.Is<Session>(s => s.IsUnblinded == true)), Times.Once);
    }

    [Fact]
    public async Task UnblindSession_MissingInstructorCode_Returns401()
    {
        var repo = BuildRepo();
        var context = BuildContext(null);

        var result = await SessionEndpointHandlers.UnblindSession(Guid.NewGuid(), context, repo.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task UnblindSession_UnknownSessionId_Returns404()
    {
        var repo = BuildRepo(null);
        var context = BuildContext("INST001");

        var result = await SessionEndpointHandlers.UnblindSession(Guid.NewGuid(), context, repo.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task UnblindSession_WrongInstructorCode_Returns403()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Title = "Test", InstructorCode = "RIGHTCODE" };
        var repo = BuildRepo(session);
        var context = BuildContext("WRONGCODE");

        var result = await SessionEndpointHandlers.UnblindSession(sessionId, context, repo.Object);

        var statusResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
