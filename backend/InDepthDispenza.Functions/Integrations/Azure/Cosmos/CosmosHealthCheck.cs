using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public class CosmosHealthCheck : IHealthCheck
{
    private readonly IOptions<CosmosDbOptions> _options;
    private readonly ILogger<CosmosHealthCheck> _logger;

    public CosmosHealthCheck(IOptions<CosmosDbOptions> options, ILogger<CosmosHealthCheck> logger)
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
            if (string.IsNullOrWhiteSpace(options.AccountEndpoint))
            {
                _logger.LogWarning("Cosmos DB health check failed: AccountEndpoint is not configured");
                return HealthCheckResult.Unhealthy("CosmosDb AccountEndpoint is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.AccountKey))
            {
                _logger.LogWarning("Cosmos DB health check failed: AccountKey is not configured");
                return HealthCheckResult.Unhealthy("CosmosDb AccountKey is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.DatabaseName))
            {
                _logger.LogWarning("Cosmos DB health check failed: DatabaseName is not configured");
                return HealthCheckResult.Unhealthy("CosmosDb DatabaseName is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.TranscriptCacheContainer))
            {
                _logger.LogWarning("Cosmos DB health check failed: TranscriptCacheContainer is not configured");
                return HealthCheckResult.Unhealthy("CosmosDb TranscriptCacheContainer is not configured");
            }

            // 2. Test Cosmos DB connectivity
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true
            };

            using var cosmosClient = new CosmosClient(
                options.AccountEndpoint,
                options.AccountKey,
                clientOptions);

            // Test connectivity by reading database properties (simpler than ReadThroughputAsync)
            var database = cosmosClient.GetDatabase(options.DatabaseName);
            var response = await database.ReadAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Cosmos DB health check succeeded");
            return HealthCheckResult.Healthy(
                "Cosmos DB is accessible and configured correctly",
                new Dictionary<string, object>
                {
                    { "databaseName", options.DatabaseName },
                    { "containerName", options.TranscriptCacheContainer },
                    { "statusCode", (int)response.StatusCode }
                });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Cosmos DB health check failed: Database not found");
            return HealthCheckResult.Degraded(
                $"Cosmos DB database does not exist yet: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "statusCode", ex.StatusCode }
                });
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB health check failed: API error");
            return HealthCheckResult.Unhealthy(
                $"Cosmos DB error: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "statusCode", ex.StatusCode },
                    { "activityId", ex.ActivityId }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cosmos DB health check failed: Unexpected error");
            return HealthCheckResult.Unhealthy(
                $"Cosmos DB health check failed: {ex.Message}",
                ex);
        }
    }
}
