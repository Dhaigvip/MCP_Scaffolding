using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.Models;
namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddGemini(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddHttpClient("Gemini", http =>
{
    http.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});

        services.AddKeyedSingleton<IAgentModel>("gemini", (sp, _) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var apiKey = configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Gemini API key not configured.");

            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GeminiAgentModel(factory.CreateClient("Gemini"), apiKey.Trim());
        });

        return services;
    }
}