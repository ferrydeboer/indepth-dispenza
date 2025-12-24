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
    public WireMockContainer WireMockContainer { get; }

    public WireMockConfiguration(WireMockContainer wireMockContainer)
    {
        WireMockContainer = wireMockContainer;
        _adminClient = wireMockContainer.CreateWireMockAdminClient();
        _fixture = new Fixture();
    }

    public GrokMockServer Grok => new(this);

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

        // Mapping for playlist items (snippet + optional contentDetails)
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
                                // Accept "snippet" or "snippet, contentDetails" (with optional space)
                                Name = "RegexMatcher",
                                Pattern = "^snippet(,\\s*contentDetails)?$"
                            }
                        }
                    }
                }
            },
            Response = new ResponseModel
            {
                StatusCode = statusCode ?? 200,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(response)
            }
        };

        await _adminClient.PostMappingAsync(mappingModel);

        // Mapping for videos details (contentDetails.duration)
        var idList = string.Join(",", videos.Select(v => v.VideoId));
        var videosResponse = new
        {
            items = videos.Select((video, index) => new
            {
                id = video.VideoId,
                contentDetails = new
                {
                    // Generate deterministic-ish ISO8601 durations between ~1 and ~20 minutes
                    duration = $"PT{(index % 20) + 1}M{(index * 7 % 55)}S"
                }
            }).ToList()
        };

        var videosMapping = new MappingModel
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
                            Pattern = "/youtube/v3/videos"
                        }
                    }
                },
                Methods = new[] { "GET" },
                Params = new[]
                {
                    new ParamModel
                    {
                        Name = "part",
                        Matchers = new[]
                        {
                            new MatcherModel
                            {
                                Name = "ExactMatcher",
                                Pattern = "contentDetails"
                            }
                        }
                    },
                    new ParamModel
                    {
                        Name = "id",
                        Matchers = new[]
                        {
                            new MatcherModel
                            {
                                // Match the exact comma-separated list as sent by the service
                                Name = "ExactMatcher",
                                Pattern = idList
                            }
                        }
                    }
                }
            },
            Response = new ResponseModel
            {
                StatusCode = statusCode ?? 200,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(videosResponse)
            }
        };

        await _adminClient.PostMappingAsync(videosMapping);
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
