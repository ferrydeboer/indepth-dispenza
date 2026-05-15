using AtlasOfAlchemy.Functions.Interfaces;
using AtlasOfAlchemy.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace AtlasOfAlchemy.Functions.VideoAnalysis;

/// <summary>
/// Consider this the domain layer that does the orchestration to third party platforms implementations.
/// </summary>
public class PlaylistScanService : IPlaylistScanService
{
    private readonly IPlaylistService _playlistService;
    private readonly IQueueService _queueService;
    private readonly IEnumerable<IVideoFilter> _filters;
    private readonly ILogger<PlaylistScanService> _logger;

    public PlaylistScanService(
        IPlaylistService playlistService,
        IQueueService queueService,
        IEnumerable<IVideoFilter> filters,
        ILogger<PlaylistScanService> logger)
    {
        _playlistService = playlistService;
        _queueService = queueService;
        _filters = filters;
        _logger = logger;
    }

    public async Task<ServiceResult<int>> ScanPlaylistAsync(PlaylistScanRequest request)
    {
        _logger.LogInformation("Starting playlist scan for playlist: {PlaylistId} with limit: {Limit} and filters: {Filters}",
            request.PlaylistId, request.Limit, request.Filters?.RawFilters);

        // Retrieve and enqueue videos from provider
        var successCount = 0;
        var totalCount = 0;

        await foreach (var video in _playlistService.GetPlaylistVideosAsync(request.PlaylistId, request.Limit))
        {
            totalCount++;

            var videoWithVersion = video with { VersionLabel = request.VersionLabel };

            if (!await ShouldProcessVideoAsync(videoWithVersion, request))
            {
                continue;
            }

            try
            {
                await _queueService.EnqueueVideoAsync(videoWithVersion);
                successCount++;
            }
            catch (QueueTransientException ex)
            {
                _logger.LogWarning(ex, "Transient error enqueuing video {VideoId}. Skipping item.", videoWithVersion.VideoId);
            }
            catch (QueueMessageException ex)
            {
                _logger.LogError(ex, "Permanent message error for video {VideoId}. Skipping item.", videoWithVersion.VideoId);
            }
        }

        _logger.LogInformation("Playlist scan completed. Successfully enqueued {SuccessCount} out of {TotalCount} videos",
            successCount, totalCount);

        return ServiceResult<int>.Success(successCount);
    }

    private async Task<bool> ShouldProcessVideoAsync(VideoInfo video, PlaylistScanRequest request)
    {
        try
        {
            foreach (var filter in _filters)
            {
                if (!await filter.ShouldProcessAsync(video, request))
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing filters for video {VideoId}. Skipping video.", video.VideoId);
            return false;
        }

        return true;
    }
}