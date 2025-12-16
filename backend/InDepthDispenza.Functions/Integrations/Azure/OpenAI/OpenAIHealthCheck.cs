using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.OpenAI;

public class OpenAIHealthCheck : IHealthCheck
{
    private readonly IOptions<OpenAIOptions> _options;
    private readonly ILogger<OpenAIHealthCheck> _logger;

    public OpenAIHealthCheck(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIHealthCheck> logger)
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

            // 1. Validate configuration
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                return HealthCheckResult.Unhealthy(
                    "Azure OpenAI endpoint is not configured",
                    data: new Dictionary<string, object> { { "missingConfig", "Endpoint" } });
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return HealthCheckResult.Unhealthy(
                    "Azure OpenAI API key is not configured",
                    data: new Dictionary<string, object> { { "missingConfig", "ApiKey" } });
            }

            if (string.IsNullOrWhiteSpace(options.DeploymentName))
            {
                return HealthCheckResult.Unhealthy(
                    "Azure OpenAI deployment name is not configured",
                    data: new Dictionary<string, object> { { "missingConfig", "DeploymentName" } });
            }

            // 2. Test API connectivity with a lightweight call
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", options.ApiKey);
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Make a simple request to verify endpoint is accessible
            var endpoint = options.Endpoint.TrimEnd('/');
            var listModelsUrl = $"{endpoint}/openai/deployments?api-version=2023-05-15";

            var response = await httpClient.GetAsync(listModelsUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Azure OpenAI health check succeeded");
                return HealthCheckResult.Healthy(
                    "Azure OpenAI API is accessible",
                    new Dictionary<string, object>
                    {
                        { "endpoint", options.Endpoint },
                        { "deploymentName", options.DeploymentName },
                        { "modelVersion", options.ModelVersion ?? "default" }
                    });
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Azure OpenAI health check failed: Authentication error");
                return HealthCheckResult.Unhealthy(
                    "Azure OpenAI API authentication failed",
                    data: new Dictionary<string, object>
                    {
                        { "statusCode", (int)response.StatusCode }
                    });
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Azure OpenAI health check: Rate limited");
                return HealthCheckResult.Degraded(
                    "Azure OpenAI API is rate limited",
                    data: new Dictionary<string, object>
                    {
                        { "statusCode", (int)response.StatusCode }
                    });
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Azure OpenAI health check failed: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return HealthCheckResult.Unhealthy(
                $"Azure OpenAI API error: {response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    { "statusCode", (int)response.StatusCode },
                    { "error", errorContent }
                });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure OpenAI health check failed: HTTP error");
            return HealthCheckResult.Unhealthy(
                $"Azure OpenAI API connection error: {ex.Message}",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI health check failed: Unexpected error");
            return HealthCheckResult.Unhealthy(
                $"Azure OpenAI health check failed: {ex.Message}",
                ex);
        }
    }
}
