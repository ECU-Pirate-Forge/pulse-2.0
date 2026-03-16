using LiteDB;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseWebApiCoreServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString));
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<QuestionRepository>();

        return services;
    }
}
