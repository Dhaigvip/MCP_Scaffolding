using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.Models;
using System.Net.Http.Headers;
namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAi(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddHttpClient("OpenAI", (sp, http) =>
 {
     var config = sp.GetRequiredService<IConfiguration>();
     var apiKey = config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
     if (string.IsNullOrWhiteSpace(apiKey))
         throw new InvalidOperationException("OpenAI API key not configured.");

     http.BaseAddress = new Uri("https://api.openai.com/v1/");
     http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
 });

        services.AddKeyedSingleton<IAgentModel>("openai", (sp, _) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new OpenAiAgentModel(factory.CreateClient("OpenAI"));
        });

        return services;
    }
}