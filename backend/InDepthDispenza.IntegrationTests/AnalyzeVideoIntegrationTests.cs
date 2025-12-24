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

    [Test]
    public async Task LlmPromptContainsTranscript_WhenAnalyzingVideo()
    {
        // Given a transcript available from YouTubeTranscriptIO
        const string transcriptText = "TRANSCRIPT_TOKEN_12345";
        await SetupTranscriptResponse(TestVideoId, transcriptText);

        // And Grok chat completions is stubbed on WireMock and will return a minimal valid payload
        await SetupGrokChatCompletionsResponse();

        // When AnalyzeVideo is invoked (inside Functions container)
        var response = await InvokeAnalyzeVideoAsync(TestVideoId);

        // Then the function should return success
        response.IsSuccess.Should().BeTrue(response.Content);

        // And the Grok request body should contain the transcript text
        var grokRequests = await GetGrokRequests();
        grokRequests.Should().BeGreaterThan(0, "LLM should be called");

        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        var requests = await adminClient.GetRequestsAsync();
        var grokBodies = requests
            .Where(r => r.Request?.Path == "/v1/chat/completions" && r.Request?.Method == "POST")
            .Select(r => r.Request?.Body ?? string.Empty)
            .ToList();

        grokBodies.Should().NotBeEmpty();

        bool matched = false;
        foreach (var body in grokBodies)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in messages.EnumerateArray())
                    {
                        if (msg.TryGetProperty("content", out var contentEl))
                        {
                            // content can be a string or an array of content parts
                            if (contentEl.ValueKind == JsonValueKind.String)
                            {
                                var content = contentEl.GetString();
                                if (!string.IsNullOrEmpty(content) && content.Contains(transcriptText))
                                {
                                    matched = true;
                                    break;
                                }
                            }
                            else if (contentEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var part in contentEl.EnumerateArray())
                                {
                                    if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var textEl))
                                    {
                                        var text = textEl.GetString();
                                        if (!string.IsNullOrEmpty(text) && text.Contains(transcriptText))
                                        {
                                            matched = true;
                                            break;
                                        }
                                    }
                                    else if (part.ValueKind == JsonValueKind.String)
                                    {
                                        var text = part.GetString();
                                        if (!string.IsNullOrEmpty(text) && text.Contains(transcriptText))
                                        {
                                            matched = true;
                                            break;
                                        }
                                    }
                                }
                                if (matched) break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors and fall back to raw substring search
                if (body.Contains(transcriptText))
                {
                    matched = true;
                    break;
                }
            }
        }

        matched.Should().BeTrue("Transcript text must be included in the LLM prompt body sent to Grok");
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

    private async Task SetupGrokChatCompletionsResponse()
    {
        // Minimal Grok-compatible chat completions response with JSON content for our app to parse
        var grokResponse = new
        {
            id = "chatcmpl_test_1",
            @object = "chat.completion",
            created = 1734990000,
            model = "grok-4",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        // Return a minimal valid LLM JSON our pipeline can parse
                        content = JsonSerializer.Serialize(new
                        {
                            analysis = new
                            {
                                achievements = Array.Empty<object>(),
                                timeframe = (object?)null,
                                practices = Array.Empty<string>(),
                                sentimentScore = 0.5,
                                confidenceScore = 0.5
                            },
                            proposals = new
                            {
                                taxonomy = Array.Empty<object>()
                            }
                        })
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 100,
                completion_tokens = 10,
                total_tokens = 110
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
                            Pattern = "/v1/chat/completions"
                        }
                    }
                },
                Methods = new[] { "POST" }
            },
            Response = new ResponseModel
            {
                StatusCode = 200,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(grokResponse)
            }
        };

        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        await adminClient.PostMappingAsync(mappingModel);
    }

    private async Task<int> GetGrokRequests()
    {
        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        var requests = await adminClient.GetRequestsAsync();
        return requests.Count(r => r.Request?.Path == "/v1/chat/completions" && r.Request?.Method == "POST");
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
