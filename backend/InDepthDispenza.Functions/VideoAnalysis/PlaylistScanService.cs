using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Consider this the domain layer that does the orchestration to third party platforms implementations.
/// </summary>
public class PlaylistScanService : IPlaylistScanService
{
    private readonly IPlaylistService _playlistService;
    private readonly IQueueService _queueService;
    private readonly ILogger<PlaylistScanService> _logger;

    public PlaylistScanService(
        IPlaylistService playlistService,
        IQueueService queueService,
        ILogger<PlaylistScanService> logger)
    {
        _playlistService = playlistService;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<ServiceResult<int>> ScanPlaylistAsync(PlaylistScanRequest request)
    {
        _logger.LogInformation("Starting playlist scan for playlist: {PlaylistId} with limit: {Limit}",
            request.PlaylistId, request.Limit);

        // Retrieve and enqueue videos from provider
        var successCount = 0;
        var totalCount = 0;

        await foreach (var video in _playlistService.GetPlaylistVideosAsync(request.PlaylistId, request.Limit))
        {
            totalCount++;
            try
            {
                await _queueService.EnqueueVideoAsync(video);
                successCount++;
            }
            catch (QueueTransientException ex)
            {
                _logger.LogWarning(ex, "Transient error enqueuing video {VideoId}. Skipping item.", video.VideoId);
            }
            catch (QueueMessageException ex)
            {
                _logger.LogError(ex, "Permanent message error for video {VideoId}. Skipping item.", video.VideoId);
            }
        }

        _logger.LogInformation("Playlist scan completed. Successfully enqueued {SuccessCount} out of {TotalCount} videos",
            successCount, totalCount);

        return ServiceResult<int>.Success(successCount);
    }
}