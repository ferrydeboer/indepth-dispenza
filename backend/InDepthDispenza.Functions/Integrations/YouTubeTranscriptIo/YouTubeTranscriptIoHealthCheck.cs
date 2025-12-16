using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace InDepthDispenza.Functions.Integrations.YouTubeTranscriptIo;

public class YouTubeTranscriptIoHealthCheck : IHealthCheck
{
    private readonly IOptions<YouTubeTranscriptIoOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeTranscriptIoHealthCheck> _logger;

    public YouTubeTranscriptIoHealthCheck(
        IOptions<YouTubeTranscriptIoOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<YouTubeTranscriptIoHealthCheck> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _options.Value;

            // 1. Validate configuration (BaseUrl is optional, checked in HttpClient setup)
            // ApiToken is also optional for public videos

            // 2. Test API connectivity with a lightweight call
            var httpClient = _httpClientFactory.CreateClient("YouTubeTranscriptApi");

            // Make a test request with a well-known video ID
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transcripts")
            {
                Content = JsonContent.Create(new { ids = new[] { "dQw4w9WgXcQ" } })
            };

            if (!string.IsNullOrEmpty(options.ApiToken))
            {
                request.Headers.Add("Authorization", $"Basic {options.ApiToken}");
            }

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("YouTube Transcript IO health check succeeded");
                return HealthCheckResult.Healthy(
                    "YouTube Transcript IO API is accessible",
                    new Dictionary<string, object>
                    {
                        { "hasApiToken", !string.IsNullOrEmpty(options.ApiToken) },
                        { "baseUrl", options.BaseUrl ?? httpClient.BaseAddress?.ToString() ?? "default" }
                    });
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("YouTube Transcript IO health check: Rate limited");
                return HealthCheckResult.Degraded(
                    "YouTube Transcript IO API is rate limited",
                    data: new Dictionary<string, object>
                    {
                        { "statusCode", (int)response.StatusCode }
                    });
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("YouTube Transcript IO health check failed: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return HealthCheckResult.Unhealthy(
                $"YouTube Transcript IO API error: {response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    { "statusCode", (int)response.StatusCode },
                    { "error", errorContent }
                });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "YouTube Transcript IO health check failed: HTTP error");
            return HealthCheckResult.Unhealthy(
                $"YouTube Transcript IO API connection error: {ex.Message}",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube Transcript IO health check failed: Unexpected error");
            return HealthCheckResult.Unhealthy(
                $"YouTube Transcript IO health check failed: {ex.Message}",
                ex);
        }
    }
}
