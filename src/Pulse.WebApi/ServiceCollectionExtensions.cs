using LiteDB;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Shared.Services;
using Pulse.WebApi.Features.SurveyGeneration;

namespace Pulse.WebApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseWebApiCoreServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString));
        services.AddSingleton<IJoinCodeGenerator, JoinCodeGenerator>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<QuestionRepository>();
        services.AddSingleton<QuestionService>();
        services.AddSingleton<IQuestionBankRepository, QuestionBankRepository>();
        services.AddSingleton<IResponseRepository, ResponseRepository>();
        services.AddSingleton<DeviceIdValidationService>();
        services.AddSingleton<ISessionEventPublisher, SignalRSessionEventPublisher>();
        services.AddSingleton<TriviaRepository>();
        services.AddSingleton<FormRepository>();
        services.AddSingleton<TriviaScoringService>();
        services.AddSingleton<SurveyGenerationRepository>();
        services.AddSingleton<DocumentTextExtractor>();
        #pragma warning disable EXTEXP0001 // Local inference needs a longer timeout without automatic POST retries.
        services.AddHttpClient<OllamaSurveyGenerator>((provider, client) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var address = new Uri(configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
            if (!address.IsLoopback || address.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Ollama:BaseUrl must be a local loopback HTTP endpoint.");
            client.BaseAddress = address;
            client.Timeout = TimeSpan.FromMinutes(5);
        }).RemoveAllResilienceHandlers();
        #pragma warning restore EXTEXP0001

        return services;
    }
}
