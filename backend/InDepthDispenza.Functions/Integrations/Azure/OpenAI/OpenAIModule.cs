using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InDepthDispenza.Functions.Integrations.Azure.OpenAI;

// ReSharper disable once UnusedType.Global
// Azure OpenAI model deployment was given troubles. Abandoned for now in facvour of Grok with API Key.
public static class OpenAIModule
{
    /// <summary>
    /// Adds Azure OpenAI module with all required services, configuration, and health checks.
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <param name="configuration">The configuration containing Azure OpenAI settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAIModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Load configuration
        services.Configure<OpenAIOptions>(configuration.GetSection("AzureOpenAI"));

        // 2. Register HTTP client for Azure OpenAI
        services.AddHttpClient("AzureOpenAI", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var endpoint = config["AzureOpenAI:Endpoint"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                client.BaseAddress = new Uri(endpoint);
            }
        });

        // 3. Register services
        services.AddScoped<ILlmService, OpenAILlmService>();

        // 4. Register health check
        services.AddHealthChecks()
            .AddCheck<OpenAIHealthCheck>("openai");

        return services;
    }
}
