using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Xml;

namespace InDepthDispenza.Functions.Integrations.YouTube;

public class YouTubePlaylistVideoService : IPlaylistService
{
    private readonly ILogger<YouTubePlaylistVideoService> _logger;
    private readonly YouTubeService _youTubeClient;

    public YouTubePlaylistVideoService(IOptions<YouTubeOptions> options, ILogger<YouTubePlaylistVideoService> logger)
    {
        _logger = logger;
        var youTubeOptions = options.Value;

        var apiKey = youTubeOptions.ApiKey ?? throw new InvalidOperationException("YouTube ApiKey configuration is missing");

        var initializer = new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "InDepthDispenza"
        };

        // Override base URL if provided (useful for testing with mock servers)
        if (!string.IsNullOrWhiteSpace(youTubeOptions.ApiBaseUrl))
        {
            initializer.BaseUri = youTubeOptions.ApiBaseUrl;
        }

        _youTubeClient = new YouTubeService(initializer);
    }

    public async IAsyncEnumerable<VideoInfo> GetPlaylistVideosAsync(string playlistId, int? limit = null, Func<VideoInfo, bool>? filter = null)
    {
        _logger.LogInformation("Starting to retrieve playlist videos for playlist: {PlaylistId}, limit: {Limit}", playlistId, limit);

        var request = _youTubeClient.PlaylistItems.List("snippet, contentDetails");
        request.PlaylistId = playlistId;
        request.MaxResults = Math.Min(limit ?? 50, 50); // YouTube API limit is 50

        string? nextPageToken = null;
        int retrievedCount = 0;
        bool DefaultFilter(VideoInfo vid) => vid.Description != "This video is private.";
        var predicate = (filter != null) ? video => DefaultFilter(video) && filter(video): (Func<VideoInfo, bool>?)DefaultFilter;

        do
        {
            request.PageToken = nextPageToken;
            var response = await request.ExecuteAsync();

            // Enrich with durations via Videos API (contentDetails)
            var ids = response.Items.Select(i => i.Snippet?.ResourceId?.VideoId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var durationById = new Dictionary<string, TimeSpan?>();
            if (ids.Count > 0)
            {
                var videosReq = _youTubeClient.Videos.List("contentDetails");
                videosReq.Id = string.Join(",", ids);
                var videosResp = await videosReq.ExecuteAsync();
                foreach (var v in videosResp.Items)
                {
                    var iso = v.ContentDetails?.Duration;
                    TimeSpan? ts = null;
                    if (!string.IsNullOrWhiteSpace(iso))
                    {
                        try { ts = XmlConvert.ToTimeSpan(iso); }
                        catch { ts = null; }
                    }
                    if (!string.IsNullOrWhiteSpace(v.Id))
                        durationById[v.Id] = ts;
                }
            }

            foreach (var item in response.Items)
            {
                var videoId = item.Snippet?.ResourceId?.VideoId ?? string.Empty;
                durationById.TryGetValue(videoId, out var duration);
                var video = CreateVideoInfo(item, duration);
                if (!predicate(video)) continue;

                yield return video;
                retrievedCount++;
                if (limit.HasValue && retrievedCount >= limit.Value)
                    yield break;
            }

            nextPageToken = response.NextPageToken;
            // Continue if we have more pages and haven't reached the limit
        } while (!string.IsNullOrEmpty(nextPageToken) &&
                 (!limit.HasValue || retrievedCount < limit.Value));

        _logger.LogInformation("Successfully retrieved {Count} videos from playlist {PlaylistId}", retrievedCount, playlistId);
    }

    private static VideoInfo CreateVideoInfo(PlaylistItem item, TimeSpan? duration = null)
    {
        var snippet = item.Snippet;
        return new VideoInfo(
            VideoId: snippet.ResourceId.VideoId,
            Title: snippet.Title,
            Description: snippet.Description,
            ChannelTitle: snippet.ChannelTitle,
            PublishedAt: snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue,
            ThumbnailUrl: snippet.Thumbnails?.Default__?.Url ?? string.Empty,
            Duration: duration
        );
    }
}
