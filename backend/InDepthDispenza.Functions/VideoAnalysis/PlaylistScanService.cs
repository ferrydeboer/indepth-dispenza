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

            // Retrieve videos from YouTube
            var videosResult = await _playlistService.GetPlaylistVideosAsync(request.PlaylistId, request.Limit);
            if (!videosResult.IsSuccess)
            {
                return ServiceResult<int>.Failure(videosResult.ErrorMessage!, videosResult.Exception);
            }

            var videos = videosResult.Data!.ToList();

            // Enqueue each video
            var successCount = 0;
            var tasks = videos.Select(async video =>
            {
                var result = await _queueService.EnqueueVideoAsync(video);
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    _logger.LogWarning("Failed to enqueue video {VideoId}: {Error}", 
                        video.VideoId, result.ErrorMessage);
                }
                return result;
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Playlist scan completed. Successfully enqueued {SuccessCount} out of {TotalCount} videos", 
                successCount, videos.Count);

            return ServiceResult<int>.Success(successCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during playlist scan for playlist: {PlaylistId}", request.PlaylistId);
            return ServiceResult<int>.Failure($"Playlist scan failed: {ex.Message}", ex);
        }
    }
}