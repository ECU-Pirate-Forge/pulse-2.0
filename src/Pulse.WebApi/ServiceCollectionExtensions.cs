using LiteDB;
using Pulse.Shared.Repositories;
using Pulse.Shared.Services;

namespace Pulse.WebApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseWebApiCoreServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(connectionString));
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<IJoinCodeGenerator, JoinCodeGenerator>();
        return services;
    }
}
