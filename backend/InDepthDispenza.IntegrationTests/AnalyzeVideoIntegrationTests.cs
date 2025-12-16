using System.Text.Json;
using FluentAssertions;
using InDepthDispenza.IntegrationTests.Infrastructure;
using WireMock.Admin.Mappings;

namespace InDepthDispenza.IntegrationTests;

[TestFixture]
public class AnalyzeVideoIntegrationTests : IntegrationTestBase
{
    private const string TestVideoId = "dQw4w9WgXcQ";

    [Test]
    public async Task TranscriptIsCached_WhenFetchedSuccessfully()
    {
        // Given a video with ID dQw4w9WgXcQ
        // And the YouTube Transcript API returns a transcript
        await SetupTranscriptResponse(TestVideoId, "This is a test transcript from YouTube.");

        // When I analyze the video for the first time
        var firstResponse = await InvokeAnalyzeVideoAsync(TestVideoId);

        // Then the function should return success
        firstResponse.Should().NotBeNull();
        firstResponse.IsSuccess.Should().BeTrue(firstResponse.Content);
        firstResponse.StatusCode.Should().Be(200);

        // And the transcript should be cached in Cosmos DB
        // When I analyze the same video again
        var secondResponse = await InvokeAnalyzeVideoAsync(TestVideoId);

        // Then the function should return success
        secondResponse.Should().NotBeNull();
        secondResponse.IsSuccess.Should().BeTrue(secondResponse.Content);
        secondResponse.StatusCode.Should().Be(200);

        // And the transcript should be fetched from cache (verified by checking that only 1 call was made to WireMock)
        var requests = await GetTranscriptApiRequests(TestVideoId);
        requests.Should().Be(1, "the transcript should be cached after the first fetch");
    }

    [Test]
    public async Task TranscriptCacheIsPersistent_AcrossMultipleRequests()
    {
        // Given a video with a cached transcript
        const string transcriptText = "This is a cached transcript";
        await SetupTranscriptResponse(TestVideoId, transcriptText);

        // When I analyze the video multiple times
        var response1 = await InvokeAnalyzeVideoAsync(TestVideoId);
        var response2 = await InvokeAnalyzeVideoAsync(TestVideoId);
        var response3 = await InvokeAnalyzeVideoAsync(TestVideoId);

        // Then all requests should succeed
        response1.IsSuccess.Should().BeTrue();
        response2.IsSuccess.Should().BeTrue();
        response3.IsSuccess.Should().BeTrue();

        // And the transcript API should only be called once
        var requests = await GetTranscriptApiRequests(TestVideoId);
        requests.Should().Be(1, "subsequent requests should use the cache");
    }

    [Test]
    public async Task EmptyTranscriptIsHandled_WhenVideoHasNoCaptions()
    {
        // Given a video without transcripts
        await SetupTranscriptResponse(TestVideoId, string.Empty);

        // When I analyze the video
        var response = await InvokeAnalyzeVideoAsync(TestVideoId);

        // Then the function should return success (but with empty transcript)
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue(response.Content);
        response.StatusCode.Should().Be(200);

        // And the response should indicate no transcript
        var content = JsonSerializer.Deserialize<AnalyzeVideoResponseContent>(response.Content);
        content.Should().NotBeNull();
        content!.transcriptLength.Should().Be(0);
    }

    private async Task SetupTranscriptResponse(string videoId, string transcript)
    {
        // YouTube Transcript IO API response format (matches actual API structure)
        var response = new[]
        {
            new
            {
                id = videoId,
                title = "Test Video",
                text = transcript,
                tracks = new[]
                {
                    new
                    {
                        language = "en",
                        transcript = string.IsNullOrEmpty(transcript)
                            ? Array.Empty<object>()
                            : transcript.Split(' ').Select((word, index) => new
                            {
                                start = (index * 0.5m).ToString(),
                                dur = "0.5",
                                text = word
                            }).ToArray()
                    }
                },
                languages = new[]
                {
                    new { label = "English", languageCode = "en" }
                },
                isLive = false,
                isLoginRequired = false,
                microformat = new
                {
                    playerMicroformatRenderer = new
                    {
                        title = new { simpleText = "Test Video" },
                        description = new { simpleText = "Test description" },
                        lengthSeconds = "120",
                        ownerChannelName = "Test Channel",
                        category = "Education",
                        publishDate = "2024-01-01",
                        externalChannelId = "UCtest123"
                    }
                },
                playabilityStatus = new
                {
                    status = "OK",
                    reason = ""
                }
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
                            Name = "ExactMatcher",
                            Pattern = "/api/transcripts"
                        }
                    }
                },
                Methods = new[] { "POST" },
                Body = new BodyModel
                {
                    Matcher = new MatcherModel
                    {
                        Name = "JsonMatcher",
                        Pattern = $"{{ \"ids\": [ \"{videoId}\" ] }}"
                    }
                }
            },
            Response = new ResponseModel
            {
                StatusCode = 200,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(response)
            }
        };

        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        await adminClient.PostMappingAsync(mappingModel);
    }

    private async Task<AnalyzeVideoResponse> InvokeAnalyzeVideoAsync(string videoId)
    {
        var httpResponse = await HttpClient.GetAsync($"/api/AnalyzeVideo?videoId={videoId}");

        return new AnalyzeVideoResponse(
            IsSuccess: httpResponse.IsSuccessStatusCode,
            StatusCode: (int)httpResponse.StatusCode,
            Content: await httpResponse.Content.ReadAsStringAsync());
    }

    private async Task<int> GetTranscriptApiRequests(string videoId)
    {
        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        var requests = await adminClient.GetRequestsAsync();

        return requests
            .Where(r => r.Request?.Path == "/api/transcripts"
                     && r.Request?.Method == "POST"
                     && r.Request?.Body?.Contains(videoId) == true)
            .Count();
    }

    private record AnalyzeVideoResponse(bool IsSuccess, int StatusCode, string Content);

    private record AnalyzeVideoResponseContent(
        string videoId,
        string title,
        int transcriptLength,
        int wordCount,
        string language,
        bool isEmpty,
        bool readyForLlm,
        string message
    );
}
