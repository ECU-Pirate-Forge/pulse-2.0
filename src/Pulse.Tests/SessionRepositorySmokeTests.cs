using Microsoft.Extensions.DependencyInjection;

namespace Pulse.Tests.Tests;

public class SessionRepositorySmokeTests
{
    [Fact]
    public void AddPulseWebApiCoreServices_RegistersSessionRepository()
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
