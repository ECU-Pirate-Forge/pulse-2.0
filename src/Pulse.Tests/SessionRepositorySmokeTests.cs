using Microsoft.Extensions.DependencyInjection;
using Pulse.Shared.Models;
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

    [Fact]
    public void SessionRepository_InsertThenGetById_PersistsAndRetrievesSession()
    {
        var services = new ServiceCollection();
        services.AddPulseWebApiCoreServices("Filename=:memory:");

        using var serviceProvider = services.BuildServiceProvider();
        var repo = serviceProvider.GetRequiredService<ISessionRepository>();

        var session = new Session
        {
            Title = "Week 1 Review",
            InstructorCode = "ABC123",
            JoinCode = "JOIN12"
        };

        var inserted = repo.Insert(session);
        var retrieved = repo.GetById(inserted.Id);
        var missing = repo.GetById(Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, inserted.Id);
        Assert.NotEqual(default, inserted.CreatedAt);
        Assert.NotEqual(default, inserted.UpdatedAt);
        Assert.Equal("Draft", inserted.Status);

        Assert.NotNull(retrieved);
        Assert.Equal(inserted.Id, retrieved!.Id);
        Assert.Equal("Week 1 Review", retrieved.Title);
        Assert.Equal("ABC123", retrieved.InstructorCode);
        Assert.Equal("JOIN12", retrieved.JoinCode);
        Assert.Equal("Draft", retrieved.Status);

        Assert.Null(missing);
    }
}
