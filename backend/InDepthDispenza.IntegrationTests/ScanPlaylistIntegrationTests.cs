using AutoFixture;
using Azure.Storage.Queues.Models;
using FluentAssertions;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.IntegrationTests.Infrastructure;
using System.Text.Json;

namespace InDepthDispenza.IntegrationTests;

[TestFixture]
public class ScanPlaylistIntegrationTests : IntegrationTestBase
{
    private const string TestPlaylistId = "T3sTV1d305";

    [Test]
    public async Task VideosQueuedSuccessfully_WhenPlaylistScannedWithLimit()
    {
        // Given a YouTube Playlist with ID T3sTV1d305 with 100 videos
        const int totalVideos = 10;
        const int limit = 3;

        await WireMockConfig.SetupPlaylistResponse(TestPlaylistId, totalVideos);

        // When I scan that playlist through the `ScanPlaylist` endpoint
        // And I use a limit of 3
        var response = await InvokeScanPlaylistAsync(TestPlaylistId, limit);

        // Then the `ScanPlaylist` should return a successful response
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue(response.Content);
        response.StatusCode.Should().Be(200);

        // And 3 messages should be stored on the Queue "videos"
        await OutputCurrentQueueStateAsync(); // Debug output
        var queueProperties = await QueueClient.GetPropertiesAsync();
        queueProperties.Value.ApproximateMessagesCount.Should().Be(limit);

        // And the IDs should match the first three
        var messages = await GetQueueMessagesAsync(limit);
        messages.Should().HaveCount(limit);

        var videoIds = messages.Select(m => m.VideoId).ToList();
        videoIds.Should().HaveCount(limit);
        videoIds.Should().OnlyContain(id => !string.IsNullOrWhiteSpace(id));
    }

    [Test]
    public async Task VideosQueuingFailed_WhenApiRespondsWithError()
    {
        // Given a YouTube Playlist with ID T3sTV1d305 with 100 videos
        // And the API responds with a 429 failure with message "API Limit Reached"
        const int statusCode = 429;
        const string errorMessage = "API Limit Reached";

        await WireMockConfig.SetupPlaylistErrorResponse(TestPlaylistId, statusCode, errorMessage);

        // When I scan that playlist through the `ScanPlaylist` endpoint
        var response = await InvokeScanPlaylistAsync(TestPlaylistId, null);

        // Then the `ScanPlaylist` should return an unsuccessful response
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().BeGreaterThanOrEqualTo(400);
    }

    private async Task<ScanPlaylistResponse> InvokeScanPlaylistAsync(string playlistId, int? limit)
    {
        var queryString = $"?playlistId={playlistId}";
        if (limit.HasValue)
        {
            queryString += $"&limit={limit.Value}";
        }

        var httpResponse = await HttpClient.GetAsync($"/api/ScanPlaylist{queryString}");

        return new ScanPlaylistResponse(
            IsSuccess: httpResponse.IsSuccessStatusCode,
            StatusCode: (int)httpResponse.StatusCode,
            Content: await httpResponse.Content.ReadAsStringAsync());
    }

    private async Task<List<VideoInfo>> GetQueueMessagesAsync(int maxMessages)
    {
        var messages = await QueueClient.ReceiveMessagesAsync(maxMessages);
        var videoInfos = new List<VideoInfo>();

        foreach (var message in messages.Value)
        {
            var videoInfo = JsonSerializer.Deserialize<VideoInfo>(message.MessageText);
            if (videoInfo != null)
            {
                videoInfos.Add(videoInfo);
            }
        }

        return videoInfos;
    }

    private record ScanPlaylistResponse(bool IsSuccess, int StatusCode, string Content);
}
