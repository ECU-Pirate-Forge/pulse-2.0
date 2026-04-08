using LiteDB;
using Pulse.Common.Services;
using Pulse.Shared.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseWebApiCoreServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString));
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<QuestionRepository>();
        services.AddSingleton<ResponseRepository>();

        return services;
    }
}
