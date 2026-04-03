using LiteDB;
using Pulse.Common.Services;
using Pulse.Shared.Services;

namespace Pulse.WebApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseWebApiCoreServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString));
        services.AddSingleton<IJoinCodeGenerator, JoinCodeGenerator>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<QuestionRepository>();

        return services;
    }
}
