using Pulse.WebApi;
using Pulse.Shared.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Common.Services;

namespace Pulse.Tests.Tests;

public class SessionRepositorySmokeTests
{
    [Fact]
    public void AddPulseWebApiCoreServicesRegistersSessionRepository()
    {
        // Use an in-memory LiteDB to keep this test isolated and fast.
        var services = new ServiceCollection();
        services.AddPulseWebApiCoreServices("Filename=:memory:");

        using var serviceProvider = services.BuildServiceProvider();
        var repo = serviceProvider.GetRequiredService<ISessionRepository>();

        Assert.NotNull(repo);
        Assert.IsType<SessionRepository>(repo);
    }
}
