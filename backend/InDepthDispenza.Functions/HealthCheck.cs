using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions;

public class HealthCheck
{
    private readonly ILogger<HealthCheck> _logger;
    private readonly HealthCheckService _healthCheckService;

    public HealthCheck(ILogger<HealthCheck> logger, HealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check endpoint called");

        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var result = new
            {
                status = healthReport.Status.ToString(),
                totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                entries = healthReport.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                })
            };

            // Set HTTP status based on health status
            response.StatusCode = healthReport.Status switch
            {
                HealthStatus.Healthy => HttpStatusCode.OK,
                HealthStatus.Degraded => HttpStatusCode.OK, // 200 but with degraded status
                HealthStatus.Unhealthy => HttpStatusCode.ServiceUnavailable,
                _ => HttpStatusCode.InternalServerError
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing health checks");

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                status = "Unhealthy",
                error = ex.Message
            }));

            return response;
        }
    }
}
