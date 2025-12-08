using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InDepthDispenza.Functions.Integrations;

/// <summary>
/// Queue services for mere debugging purposes. Simply outputs the video as JSON to a debug log.
/// </summary>
public class LoggingQueueService(ILogger<LoggingQueueService> logger) : IQueueService
{
    public Task<ServiceResult> EnqueueVideoAsync(VideoInfo video)
    {
        string message = JsonConvert.SerializeObject(video);
        logger.LogInformation(message);
        return Task.FromResult (ServiceResult.Success());
    }
}