using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InDepthDispenza.Functions.Integrations.YouTube;

public static class YouTubeModule
{
    /// <summary>
    /// Adds YouTube module with all required services, configuration, and health checks.
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <param name="configuration">The configuration containing YouTube settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddYouTubeModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Load configuration
        services.Configure<YouTubeOptions>(configuration.GetSection("YouTube"));

        // 2. Register services
        services.AddScoped<IPlaylistService, YouTubePlaylistVideoService>();

        // 3. Register health check
        services.AddHealthChecks()
            .AddCheck<YouTubeHealthCheck>("youtube");

        return services;
    }
}
