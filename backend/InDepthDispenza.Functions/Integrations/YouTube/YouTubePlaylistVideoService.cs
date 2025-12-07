using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.Integrations.YouTube;

public class PlaylistPlaylistService : IPlaylistService
{
    private readonly ILogger<YouTubeService> _logger;
    private readonly YouTubeService _youTubeClient;
    private readonly YouTubeOptions _options;

    public PlaylistPlaylistService(IOptions<YouTubeOptions> options, ILogger<YouTubeService> logger)
    {
        _logger = logger;
        _options = options.Value;
        
        var apiKey = _options.ApiKey ?? throw new InvalidOperationException("YouTube ApiKey configuration is missing");
        
        _youTubeClient = new YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = apiKey,
            ApplicationName = "InDepthDispenza"
        });
    }

    public async Task<ServiceResult<IEnumerable<VideoInfo>>> GetPlaylistVideosAsync(string playlistId, int? limit = null)
    {
        try
        {
            _logger.LogInformation("Starting to retrieve playlist videos for playlist: {PlaylistId}, limit: {Limit}", playlistId, limit);

            var videos = new List<VideoInfo>();
            var request = _youTubeClient.PlaylistItems.List("snippet");
            request.PlaylistId = playlistId;
            request.MaxResults = Math.Min(limit ?? 50, 50); // YouTube API limit is 50
            
            string? nextPageToken = null;
            int retrievedCount = 0;

            do
            {
                request.PageToken = nextPageToken;
                var response = await request.ExecuteAsync();

                foreach (var item in response.Items)
                {
                    if (limit.HasValue && retrievedCount >= limit.Value)
                        break;

                    var video = CreateVideoInfo(item);
                    videos.Add(video);
                    retrievedCount++;
                }

                nextPageToken = response.NextPageToken;
                
                // Continue if we have more pages and haven't reached the limit
            } while (!string.IsNullOrEmpty(nextPageToken) && 
                     (!limit.HasValue || retrievedCount < limit.Value));

            _logger.LogInformation("Successfully retrieved {Count} videos from playlist {PlaylistId}", videos.Count, playlistId);
            return ServiceResult<IEnumerable<VideoInfo>>.Success(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving playlist videos for playlist: {PlaylistId}", playlistId);
            return ServiceResult<IEnumerable<VideoInfo>>.Failure(
                $"Failed to retrieve playlist videos: {ex.Message}", ex);
        }
    }

    private static VideoInfo CreateVideoInfo(PlaylistItem item)
    {
        var snippet = item.Snippet;
        return new VideoInfo(
            VideoId: snippet.ResourceId.VideoId,
            Title: snippet.Title,
            Description: snippet.Description,
            ChannelTitle: snippet.ChannelTitle,
            PublishedAt: snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue,
            ThumbnailUrl: snippet.Thumbnails?.Default__?.Url ?? string.Empty
        );
    }
}
