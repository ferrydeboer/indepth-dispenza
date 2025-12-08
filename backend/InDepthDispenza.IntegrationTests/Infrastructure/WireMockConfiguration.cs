    using System.Text.Json;
using AutoFixture;
using InDepthDispenza.Functions.Interfaces;
using WireMock.Admin.Mappings;
using WireMock.Client;
using WireMock.Net.Testcontainers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public class WireMockConfiguration
{
    private readonly IWireMockAdminApi _adminClient;
    private readonly Fixture _fixture;

    public WireMockConfiguration(WireMockContainer wireMockContainer)
    {
        _adminClient = wireMockContainer.CreateWireMockAdminClient();
        _fixture = new Fixture();
    }

    public async Task SetupPlaylistResponse(string playlistId, int videoCount, int? statusCode = 200)
    {
        var videos = _fixture.Build<VideoInfo>()
            .With(v => v.VideoId, () => _fixture.Create<string>())
            .With(v => v.PublishedAt, () => DateTimeOffset.UtcNow.AddDays(-_fixture.Create<int>() % 365))
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
                    publishedAt = video.PublishedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
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

        var mappingModel = new MappingModel
        {
            Request = new RequestModel
            {
                Path = new PathModel
                {
                    Matchers = new[]
                    {
                        new MatcherModel
                        {
                            Name = "WildcardMatcher",
                            Pattern = "/youtube/v3/playlistItems"
                        }
                    }
                },
                Methods = new[] { "GET" },
                Params = new[]
                {
                    new ParamModel
                    {
                        Name = "playlistId",
                        Matchers = new[]
                        {
                            new MatcherModel
                            {
                                Name = "ExactMatcher",
                                Pattern = playlistId
                            }
                        }
                    },
                    new ParamModel
                    {
                        Name = "part",
                        Matchers = new[]
                        {
                            new MatcherModel
                            {
                                Name = "ExactMatcher",
                                Pattern = "snippet"
                            }
                        }
                    }
                }
            },
            Response = new ResponseModel
            {
                StatusCode = statusCode.Value,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(response)
            }
        };

        await _adminClient.PostMappingAsync(mappingModel);
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

        var mappingModel = new MappingModel
        {
            Request = new RequestModel
            {
                Path = new PathModel
                {
                    Matchers = new[]
                    {
                        new MatcherModel
                        {
                            Name = "WildcardMatcher",
                            Pattern = "/youtube/v3/playlistItems"
                        }
                    }
                },
                Methods = new[] { "GET" },
                Params = new[]
                {
                    new ParamModel
                    {
                        Name = "playlistId",
                        Matchers = new[]
                        {
                            new MatcherModel
                            {
                                Name = "ExactMatcher",
                                Pattern = playlistId
                            }
                        }
                    }
                }
            },
            Response = new ResponseModel
            {
                StatusCode = statusCode,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(errorResponse)
            }
        };

        await _adminClient.PostMappingAsync(mappingModel);
    }

    public async Task Reset()
    {
        await _adminClient.DeleteMappingsAsync();
    }
}
