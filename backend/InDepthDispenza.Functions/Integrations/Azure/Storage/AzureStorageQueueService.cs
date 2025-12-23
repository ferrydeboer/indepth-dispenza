using System.Text.Json;
using Azure.Storage.Queues;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Storage;

public class AzureStorageQueueService : IQueueService
{
    private readonly ILogger<AzureStorageQueueService> _logger;
    private readonly QueueClient _queueClient;

    public AzureStorageQueueService(IOptions<StorageOptions> options, ILogger<AzureStorageQueueService> logger)
    {
        _logger = logger;
        var storageOptions = options.Value;

        var connectionString = storageOptions.AzureWebJobsStorage ??
                              throw new InvalidOperationException("AzureWebJobsStorage configuration is missing");
        var queueName = storageOptions.VideoQueueName;

        var opt = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };
        _queueClient = new QueueClient(connectionString, queueName, opt);
    }

    public async Task<ServiceResult> EnqueueVideoAsync(VideoInfo video)
    {
        try
        {
            _logger.LogInformation("Enqueuing video: {VideoId} - {Title}", video.VideoId, video.Title);

            // Ensure queue exists
            await _queueClient.CreateIfNotExistsAsync();

            var messageContent = JsonSerializer.Serialize(video);
            await _queueClient.SendMessageAsync(messageContent);

            _logger.LogInformation("Successfully enqueued video: {VideoId}", video.VideoId);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing video: {VideoId}", video.VideoId);
            return ServiceResult.Failure($"Failed to enqueue video: {ex.Message}", ex);
        }
    }
}