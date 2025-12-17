using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
// using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.XAi.Grok;

public static class GrokModule
{
    /// <summary>
    /// Adds the xAI Grok module: options, HttpClient, ILlmService implementation, and health check.
    /// </summary>
    public static IServiceCollection AddGrokModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Options
        services.Configure<GrokOptions>(configuration.GetSection("Grok"));

        // 2. HttpClient
        services.AddHttpClient("Grok", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GrokOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                client.BaseAddress = new Uri(opts.BaseUrl);
            }

            // Configure HTTP timeout (default 120s if not specified)
            var timeoutSeconds = opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 120;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        // 3. Services
        services.AddScoped<ILlmService, GrokLlmService>();

        // 4. Health checks
        services.AddHealthChecks()
            .AddCheck<GrokHealthCheck>("grok");

        return services;
    }
}
