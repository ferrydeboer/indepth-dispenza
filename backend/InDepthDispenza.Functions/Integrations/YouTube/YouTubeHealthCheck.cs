using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.YouTube;

public class YouTubeHealthCheck : IHealthCheck
{
    private readonly IOptions<YouTubeOptions> _options;
    private readonly ILogger<YouTubeHealthCheck> _logger;

    public YouTubeHealthCheck(IOptions<YouTubeOptions> options, ILogger<YouTubeHealthCheck> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _options.Value;

            // 1. Validate configuration exists
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                _logger.LogWarning("YouTube health check failed: ApiKey is not configured");
                return HealthCheckResult.Unhealthy("YouTube ApiKey is not configured");
            }

            // 2. Validate API key format (basic check)
            if (options.ApiKey.Length < 20)
            {
                _logger.LogWarning("YouTube health check failed: ApiKey appears invalid");
                return HealthCheckResult.Unhealthy("YouTube ApiKey appears invalid");
            }

            // 3. Test API connectivity with a lightweight call
            var initializer = new BaseClientService.Initializer
            {
                ApiKey = options.ApiKey,
                ApplicationName = "InDepthDispenza"
            };

            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            {
                initializer.BaseUri = options.ApiBaseUrl;
            }

            using var youTubeService = new YouTubeService(initializer);

            // Make a minimal API call to verify credentials
            // Using videos.list with a known video ID as a lightweight test
            var request = youTubeService.Videos.List("snippet");
            request.Id = "dQw4w9WgXcQ"; // Use a well-known video ID for testing
            request.MaxResults = 1;

            var response = await request.ExecuteAsync(cancellationToken);

            if (response?.Items == null)
            {
                _logger.LogWarning("YouTube health check failed: API returned null response");
                return HealthCheckResult.Degraded("YouTube API returned unexpected response");
            }

            _logger.LogInformation("YouTube health check succeeded");
            return HealthCheckResult.Healthy("YouTube API is accessible and credentials are valid");
        }
        catch (Google.GoogleApiException ex)
        {
            _logger.LogError(ex, "YouTube health check failed: API error");
            return HealthCheckResult.Unhealthy(
                $"YouTube API error: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "statusCode", ex.HttpStatusCode },
                    { "error", ex.Error?.Message ?? "Unknown error" }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube health check failed: Unexpected error");
            return HealthCheckResult.Unhealthy(
                $"YouTube health check failed: {ex.Message}",
                ex);
        }
    }
}
