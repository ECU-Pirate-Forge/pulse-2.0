using Moq;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.Common.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Sessions;

public class CreateSessionTests
{
    private Mock<ISessionRepository> BuildRepo()
    {
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.JoinCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.InsertAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session s, CancellationToken _) => s);
        return repo;
    }

    private Mock<IJoinCodeGenerator> BuildGenerator(string code = "ABC123")
    {
        var gen = new Mock<IJoinCodeGenerator>();
        gen.Setup(g => g.Generate()).Returns(code);
        return gen;
    }

    private HttpContext BuildContext(string instructorCode = "INSTRUCTOR-001")
    {
        var context = new DefaultHttpContext();
        context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }

    [Fact]
    public async Task CreateSession_ValidRequest_Returns201WithCodes()
    {
        var repo = BuildRepo();
        var gen = BuildGenerator();
        var context = BuildContext();
        var request = new CreateSessionRequest { Title = "Test Session" };

        var result = await SessionEndpointHandlers.CreateSession(context, request, repo.Object, gen.Object);

        var created = Assert.IsType<Created<CreateSessionResponse>>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(created.Value?.JoinCode);
        Assert.NotNull(created.Value?.InstructorCode);
    }

    [Fact]
    public async Task CreateSession_EmptyTitle_Returns400()
    {
        var repo = BuildRepo();
        var gen = BuildGenerator();
        var context = BuildContext();
        var request = new CreateSessionRequest { Title = "" };

        var result = await SessionEndpointHandlers.CreateSession(context, request, repo.Object, gen.Object);

        Assert.IsType<BadRequest<string>>(result);
    }
}
