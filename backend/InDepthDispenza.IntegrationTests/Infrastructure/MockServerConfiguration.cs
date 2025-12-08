using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public class MockServerConfiguration
{
    private readonly HttpClient _httpClient;
    private readonly Fixture _fixture;

    public MockServerConfiguration(string mockServerUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(mockServerUrl) };
        _fixture = new Fixture();
    }

    public async Task SetupPlaylistResponse(string playlistId, int videoCount, int? statusCode = 200)
    {
        var videos = _fixture.Build<VideoInfo>()
            .With(v => v.VideoId, () => _fixture.Create<string>())
            .CreateMany(videoCount)
            .ToList();

        var response = new
        {
            items = videos.Select((video, index) => new
            {
                snippet = new
                {
                    resourceId = new
                    {
                        videoId = video.VideoId
                    },
                    title = video.Title,
                    description = video.Description,
                    channelTitle = video.ChannelTitle,
                    publishedAt = video.PublishedAt.ToString("o"),
                    thumbnails = new
                    {
                        @default = new
                        {
                            url = video.ThumbnailUrl
                        }
                    }
                }
            }).ToList()
        };

        var expectation = new
        {
            httpRequest = new
            {
                method = "GET",
                path = "/youtube/v3/playlistItems",
                queryStringParameters = new
                {
                    playlistId = new[] { playlistId },
                    part = new[] { "snippet" },
                    maxResults = new[] { "50" }
                }
            },
            httpResponse = new
            {
                statusCode = statusCode,
                body = JsonSerializer.Serialize(response)
            }
        };

        await _httpClient.PutAsJsonAsync("/mockserver/expectation", expectation);
    }

    public async Task SetupPlaylistErrorResponse(string playlistId, int statusCode, string errorMessage)
    {
        var errorResponse = new
        {
            error = new
            {
                code = statusCode,
                message = errorMessage
            }
        };

        var expectation = new
        {
            httpRequest = new
            {
                method = "GET",
                path = "/youtube/v3/playlistItems",
                queryStringParameters = new
                {
                    playlistId = new[] { playlistId }
                }
            },
            httpResponse = new
            {
                statusCode = statusCode,
                body = JsonSerializer.Serialize(errorResponse)
            }
        };

        await _httpClient.PutAsJsonAsync("/mockserver/expectation", expectation);
    }

    public async Task Reset()
    {
        await _httpClient.PutAsync("/mockserver/reset", null);
    }
}
