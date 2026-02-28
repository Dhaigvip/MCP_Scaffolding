using Anthropic;
using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.Models;
using Mcp.Agent.Routing;
using Mcp.Agent.ToolSource;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace Mcp.Agent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgent(this IServiceCollection services)
    {
        services.AddSingleton<SessionManager>();
        services.AddSingleton<IAgentModelRouter, AgentModelRouter>();
        services.AddScoped<AgentService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="PalmaAgentToolSource"/> as <see cref="IAgentToolSource"/>.
    /// Use with <c>AddPalmaMcp()</c> — tools are resolved dynamically per session.
    /// </summary>
    public static IServiceCollection AddPalmaAgentToolSource(this IServiceCollection services)
    {
        services.AddSingleton<IAgentToolSource, PalmaAgentToolSource>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="RegistryAgentToolSource"/> as <see cref="IAgentToolSource"/>.
    /// Use with <c>AddSwaggerMcp()</c> or <c>AddPalmaMcpRemote()</c> — tools come from
    /// the static <c>DynamicToolRegistry</c> populated at startup.
    /// </summary>
    public static IServiceCollection AddSwaggerAgentToolSource(this IServiceCollection services)
    {
        services.AddSingleton<IAgentToolSource, RegistryAgentToolSource>();
        return services;
    }

    public static IServiceCollection AddAnthropic(
        this IServiceCollection services,
        IConfiguration config)
    {
        var apiKey = config["Anthropic:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic API key not configured.");

        services.AddSingleton(new AnthropicClient(
            new Anthropic.Core.ClientOptions { ApiKey = apiKey.Trim() }));

        services.AddKeyedSingleton<IAgentModel>("anthropic", (sp, _) =>
        {
            var client = sp.GetRequiredService<AnthropicClient>();
            return new AnthropicAgentModel(client);
        });

        return services;
    }

    public static IServiceCollection AddOpenAi(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddHttpClient("OpenAI", (sp, http) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var apiKey = cfg["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenAI API key not configured.");

            http.BaseAddress = new Uri("https://api.openai.com/v1/");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        });

        services.AddKeyedSingleton<IAgentModel>("openai", (sp, _) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new OpenAiAgentModel(factory.CreateClient("OpenAI"));
        });

        return services;
    }

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
            var cfg = sp.GetRequiredService<IConfiguration>();
            var apiKey = cfg["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Gemini API key not configured.");

            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GeminiAgentModel(factory.CreateClient("Gemini"), apiKey.Trim());
        });

        return services;
    }
}
