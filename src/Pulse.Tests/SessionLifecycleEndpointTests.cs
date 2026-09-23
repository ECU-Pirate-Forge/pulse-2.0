using Microsoft.AspNetCore.Http;
using Moq;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public class SessionLifecycleEndpointTests
{
    [Fact]
    public async Task ActivateSession_DraftOwnedSession_TransitionsToActive()
    {
        var session = CreateSession(SessionStatus.Draft);
        var repo = CreateRepository(session);

        var result = await SessionEndpointHandlers.ActivateSession(
            session.Id,
            CreateContext(session.InstructorCode),
            repo.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<Session>>(result);
        Assert.Equal(SessionStatus.Active, session.Status);
        repo.Verify(r => r.Update(It.Is<Session>(s => s.Status == SessionStatus.Active)), Times.Once);
    }

    [Fact]
    public async Task CloseSession_ActiveOwnedSession_TransitionsToClosed()
    {
        var session = CreateSession(SessionStatus.Active);
        var repo = CreateRepository(session);

        var result = await SessionEndpointHandlers.CloseSession(
            session.Id,
            CreateContext(session.InstructorCode),
            repo.Object);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<Session>>(result);
        Assert.Equal(SessionStatus.Closed, session.Status);
    }

    [Fact]
    public async Task CloseSession_DraftSession_ReturnsConflict()
    {
        var session = CreateSession(SessionStatus.Draft);
        var repo = CreateRepository(session);

        var result = await SessionEndpointHandlers.CloseSession(
            session.Id,
            CreateContext(session.InstructorCode),
            repo.Object);

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<string>>(result);
        Assert.Contains("Draft", conflict.Value);
        Assert.Equal(SessionStatus.Draft, session.Status);
        repo.Verify(r => r.Update(It.IsAny<Session>()), Times.Never);
    }

    [Fact]
    public async Task ActivateSession_DifferentInstructor_ReturnsForbidden()
    {
        var session = CreateSession(SessionStatus.Draft);
        var repo = CreateRepository(session);

        var result = await SessionEndpointHandlers.ActivateSession(
            session.Id,
            CreateContext("OTHER"),
            repo.Object);

        var forbidden = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static Session CreateSession(SessionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Week 1 feedback",
        InstructorCode = "INST",
        Status = status
    };

    private static Mock<ISessionRepository> CreateRepository(Session session)
    {
        var repository = new Mock<ISessionRepository>();
        repository.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);
        repository.Setup(r => r.Update(It.IsAny<Session>())).Returns(true);
        return repository;
    }

    private static HttpContext CreateContext(string instructorCode)
    {
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }
}
