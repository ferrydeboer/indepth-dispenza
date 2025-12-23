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
        try
        {
            _logger.LogInformation("Starting playlist scan for playlist: {PlaylistId} with limit: {Limit}",
                request.PlaylistId, request.Limit);

            // Retrieve and enqueue videos from provider
            var successCount = 0;
            var totalCount = 0;

            await foreach (var video in _playlistService.GetPlaylistVideosAsync(request.PlaylistId, request.Limit))
            {
                totalCount++;
                var result = await _queueService.EnqueueVideoAsync(video);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to enqueue video {VideoId}: {Error}",
                        video.VideoId, result.ErrorMessage);
                }
            }

            _logger.LogInformation("Playlist scan completed. Successfully enqueued {SuccessCount} out of {TotalCount} videos",
                successCount, totalCount);

            return ServiceResult<int>.Success(successCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during playlist scan for playlist: {PlaylistId}", request.PlaylistId);
            return ServiceResult<int>.Failure($"Playlist scan failed: {ex.Message}", ex);
        }
    }
}