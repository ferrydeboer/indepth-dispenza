using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Storage;

public class StorageHealthCheck : IHealthCheck
{
    private readonly IOptions<StorageOptions> _options;
    private readonly ILogger<StorageHealthCheck> _logger;

    public StorageHealthCheck(IOptions<StorageOptions> options, ILogger<StorageHealthCheck> logger)
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
            if (string.IsNullOrWhiteSpace(options.AzureWebJobsStorage))
            {
                _logger.LogWarning("Azure Storage health check failed: AzureWebJobsStorage is not configured");
                return HealthCheckResult.Unhealthy("AzureWebJobsStorage is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.VideoQueueName))
            {
                _logger.LogWarning("Azure Storage health check failed: VideoQueueName is not configured");
                return HealthCheckResult.Unhealthy("VideoQueueName is not configured");
            }

            // 2. Test Azure Storage Queue connectivity
            var queueClient = new QueueClient(options.AzureWebJobsStorage, options.VideoQueueName);
            var properties = await queueClient.GetPropertiesAsync(cancellationToken);

            _logger.LogInformation("Azure Storage health check succeeded");
            return HealthCheckResult.Healthy(
                "Azure Storage Queue is accessible and configured correctly",
                new Dictionary<string, object>
                {
                    { "queueName", options.VideoQueueName },
                    { "messageCount", properties.Value.ApproximateMessagesCount }
                });
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(ex, "Azure Storage health check failed: Queue not found");
            return HealthCheckResult.Degraded(
                $"Azure Storage queue does not exist yet (will be created on first use): {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "statusCode", ex.Status },
                    { "errorCode", ex.ErrorCode ?? "Unknown" }
                });
        }
        catch (global::Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Storage health check failed: API error");
            return HealthCheckResult.Unhealthy(
                $"Azure Storage error: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "statusCode", ex.Status },
                    { "errorCode", ex.ErrorCode ?? "Unknown" }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Storage health check failed: Unexpected error");
            return HealthCheckResult.Unhealthy(
                $"Azure Storage health check failed: {ex.Message}",
                ex);
        }
    }
}
