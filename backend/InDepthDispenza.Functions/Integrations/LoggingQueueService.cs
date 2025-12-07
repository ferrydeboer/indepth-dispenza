using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InDepthDispenza.Functions.Integrations;

/// <summary>
/// Queue services for mere debugging purposes. Simply outputs the video as JSON to a debug log.
/// </summary>
public class LoggingQueueService : IQueueService
{
    ILogger<LoggingQueueService> _logger;

    public LoggingQueueService(ILogger<LoggingQueueService> logger)
    {
        _logger = logger;
    }

    public Task<ServiceResult> EnqueueVideoAsync(VideoInfo video)
    {
        string message = JsonConvert.SerializeObject(video);
        _logger.LogInformation(message);
        return Task.FromResult (ServiceResult.Success());
    }
}