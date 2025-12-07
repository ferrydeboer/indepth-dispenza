using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.Services;

public class AzureStorageQueueService : IQueueService
{
    private readonly ILogger<AzureStorageQueueService> _logger;
    // private readonly QueueClient _queueClient;

    public AzureStorageQueueService(IConfiguration configuration, ILogger<AzureStorageQueueService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureWebJobsStorage"] ?? 
                              throw new InvalidOperationException("AzureWebJobsStorage configuration is missing");
        var queueName = configuration["VideoQueueName"] ?? "video-processing-queue";
        
        // _queueClient = new QueueClient(connectionString, queueName);
    }

    public async Task<ServiceResult> EnqueueVideoAsync(VideoInfo video)
    {
        // try
        // {
        //     _logger.LogInformation("Enqueuing video: {VideoId} - {Title}", video.VideoId, video.Title);
        //
        //     // Ensure queue exists
        //     await _queueClient.CreateIfNotExistsAsync();
        //
        //     var messageContent = JsonSerializer.Serialize(video);
        //     await _queueClient.SendMessageAsync(messageContent);
        //
        //     _logger.LogInformation("Successfully enqueued video: {VideoId}", video.VideoId);
        //     return ServiceResult.Success();
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Error enqueuing video: {VideoId}", video.VideoId);
        //     return ServiceResult.Failure($"Failed to enqueue video: {ex.Message}", ex);
        // }
        return new ServiceResult();
    }
}