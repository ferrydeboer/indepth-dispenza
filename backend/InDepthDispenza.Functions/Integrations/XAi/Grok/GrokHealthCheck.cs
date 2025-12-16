using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.XAi.Grok;

public class GrokHealthCheck : IHealthCheck
{
    private readonly IOptions<GrokOptions> _options;
    private readonly ILogger<GrokHealthCheck> _logger;

    public GrokHealthCheck(IOptions<GrokOptions> options, ILogger<GrokHealthCheck> logger)
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
            var opt = _options.Value;
            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            {
                return HealthCheckResult.Unhealthy("Grok BaseUrl is not configured",
                    data: new Dictionary<string, object> { { "missingConfig", "BaseUrl" } });
            }
            if (string.IsNullOrWhiteSpace(opt.ApiKey))
            {
                return HealthCheckResult.Unhealthy("Grok ApiKey is not configured",
                    data: new Dictionary<string, object> { { "missingConfig", "ApiKey" } });
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opt.ApiKey);

            var url = opt.BaseUrl!.TrimEnd('/') + "/models";
            var resp = await http.GetAsync(url, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (resp.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Grok API is accessible",
                    new Dictionary<string, object>
                    {
                        { "baseUrl", opt.BaseUrl! },
                        { "model", opt.Model ?? string.Empty }
                    });
            }

            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Grok health check authentication failed");
                return HealthCheckResult.Unhealthy("Grok API authentication failed",
                    data: new Dictionary<string, object> { { "statusCode", (int)resp.StatusCode } });
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Grok health check rate limited");
                return HealthCheckResult.Degraded("Grok API is rate limited",
                    data: new Dictionary<string, object> { { "statusCode", (int)resp.StatusCode } });
            }

            _logger.LogWarning("Grok health check failed: {Status} - {Body}", resp.StatusCode, body);
            return HealthCheckResult.Unhealthy($"Grok API error: {resp.StatusCode}",
                data: new Dictionary<string, object>
                {
                    { "statusCode", (int)resp.StatusCode },
                    { "error", body }
                });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Grok health check HTTP error");
            return HealthCheckResult.Unhealthy($"Grok API connection error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Grok health check unexpected error");
            return HealthCheckResult.Unhealthy($"Grok health check failed: {ex.Message}", ex);
        }
    }
}
