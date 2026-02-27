using Anthropic;
using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.Models;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnthropic(
        this IServiceCollection services,
        IConfiguration config)
    {
        var apiKey = config["Anthropic:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic API key not configured.");

        services.AddSingleton(new AnthropicClient(
            new Anthropic.Core.ClientOptions
            {
                ApiKey = apiKey.Trim()
            }));

        services.AddKeyedSingleton<IAgentModel>("anthropic", (sp, _) =>
        {
            var anthropicClient = sp.GetRequiredService<AnthropicClient>();
            return new AnthropicAgentModel(anthropicClient);
        });

        return services;
    }
}