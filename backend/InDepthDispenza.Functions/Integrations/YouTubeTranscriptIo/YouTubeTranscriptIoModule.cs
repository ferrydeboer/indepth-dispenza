using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InDepthDispenza.Functions.Integrations.YouTubeTranscriptIo;

public static class YouTubeTranscriptIoModule
{
    /// <summary>
    /// Adds YouTube Transcript IO module with all required services, configuration, and health checks.
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <param name="configuration">The configuration containing YouTube Transcript API settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddYouTubeTranscriptIoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Load configuration
        services.Configure<YouTubeTranscriptIoOptions>(configuration.GetSection("YouTubeTranscriptApi"));

        // 2. Register HTTP client for YouTube Transcript API
        services.AddHttpClient("YouTubeTranscriptApi", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["YouTubeTranscriptApi:BaseUrl"] ?? "https://www.youtube-transcript.io";
            client.BaseAddress = new Uri(baseUrl);
        });

        // 3. Register services
        services.AddScoped<YouTubeTranscriptIoProvider>();

        // 4. Register health check
        services.AddHealthChecks()
            .AddCheck<YouTubeTranscriptIoHealthCheck>("youtubetranscriptio");

        return services;
    }
}
