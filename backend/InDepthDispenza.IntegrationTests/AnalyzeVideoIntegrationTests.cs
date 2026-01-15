using System.Net;
using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.IntegrationTests.Infrastructure;
using Microsoft.Azure.Cosmos;
using WireMock.Admin.Mappings;

namespace InDepthDispenza.IntegrationTests;

[TestFixture]
public class AnalyzeVideoIntegrationTests : IntegrationTestBase
{
    private string _testVideoId = "dQw4w9WgXcQ";

    [SetUp]
    public new void SetUp()
    {
        _testVideoId = CreateVideoId();
    }
    
    [Test]
    public async Task TranscriptIsCached_WhenFetchedSuccessfully()
    {
        // Given a video with ID dQw4w9WgXcQ
        // And the YouTube Transcript API returns a transcript
        await SetupTranscriptResponse(_testVideoId, "This is a test transcript from YouTube.");

        // When I analyze the video for the first time
        var firstResponse = await InvokeAnalyzeVideoAsync(_testVideoId);

        // Then the function should return success
        firstResponse.Should().NotBeNull();
        firstResponse.IsSuccess.Should().BeTrue(firstResponse.Content);
        firstResponse.StatusCode.Should().Be(200);

        // And the transcript should be cached in Cosmos DB
        // When I analyze the same video again
        var secondResponse = await InvokeAnalyzeVideoAsync(_testVideoId);

        // Then the function should return success
        secondResponse.Should().NotBeNull();
        secondResponse.IsSuccess.Should().BeTrue(secondResponse.Content);
        secondResponse.StatusCode.Should().Be(200);

        // And the transcript should be fetched from cache (verified by checking that only 1 call was made to WireMock)
        var requests = await GetTranscriptApiRequests(_testVideoId);
        requests.Should().Be(1, "the transcript should be cached after the first fetch");
    }

    [Test]
    public async Task TranscriptCacheIsPersistent_AcrossMultipleRequests()
    {
        // Given a video with a cached transcript
        const string transcriptText = "This is a cached transcript";
        await SetupTranscriptResponse(_testVideoId, transcriptText);

        // When I analyze the video multiple times
        var response1 = await InvokeAnalyzeVideoAsync(_testVideoId);
        var response2 = await InvokeAnalyzeVideoAsync(_testVideoId);
        var response3 = await InvokeAnalyzeVideoAsync(_testVideoId);

        // Then all requests should succeed
        response1.IsSuccess.Should().BeTrue();
        response2.IsSuccess.Should().BeTrue();
        response3.IsSuccess.Should().BeTrue();

        // And the transcript API should only be called once
        var requests = await GetTranscriptApiRequests(_testVideoId);
        requests.Should().Be(1, "subsequent requests should use the cache");
    }

    [Test]
    public async Task EmptyTranscriptIsHandled_WhenVideoHasNoCaptions()
    {
        // Given a video without transcripts
        await SetupTranscriptResponse(_testVideoId, string.Empty);

        // When I analyze the video
        var response = await InvokeAnalyzeVideoAsync(_testVideoId);

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
        string _testVideoId = CreateVideoId();
        await SetupTranscriptResponse(_testVideoId, transcriptText);

        // And Grok chat completions is stubbed on WireMock and will return a minimal valid payload
        await WireMockConfig.Grok.SetupAsync();

        // When AnalyzeVideo is invoked (inside Functions container)
        var response = await InvokeAnalyzeVideoAsync(_testVideoId);

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

    [Test]
    public async Task AchievementsFromLlm_AreStoredInCosmos()
    {
        // Given a transcript exists
        const string transcriptText = "patient reports complete remission after meditation";
        await SetupTranscriptResponse(_testVideoId, transcriptText);

        // And Grok returns an analysis with concrete achievements
        var expectedAchievements = new[]
        {
            new {
                type = "healing",
                tags = new[] { "remission", "meditation_practice" },
                details = "Complete remission after sustained meditation practice"
            },
            new {
                type = "transformation",
                tags = new[] { "mindset_shift" },
                details = "Notable positive shift in outlook"
            }
        };
        await WireMockConfig.Grok.SetupAsync(expectedAchievements);

        // When
        var response = await InvokeAnalyzeVideoAsync(_testVideoId);
        response.IsSuccess.Should().BeTrue(response.Content);

        // Then the full LLM response document should be stored in Cosmos (video-analysis container)
        var storedAchievements = await GetStoredLlmAchievementsFromCosmos(_testVideoId);

        storedAchievements.Should().NotBeNull("LLM document should be stored");
        storedAchievements!.Length.Should().Be(expectedAchievements.Length);

        // Validate content roughly matches (type/tags/details)
        for (int i = 0; i < expectedAchievements.Length; i++)
        {
            var exp = expectedAchievements[i];
            var got = storedAchievements[i];
            got.Type.Should().Be(exp.type);
            got.Tags.Should().BeEquivalentTo(exp.tags);
            got.Details.Should().Be(exp.details);
        }
    }

    private static string CreateVideoId()
    {
        var fixture = new Fixture();
        return fixture.Create<string>()[..12];
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


    private record StoredAchievement(string Type, string[] Tags, string? Details);

    // Mirror of the stored LLM doc shape in CosmosVideoAnalysisRepository
    private sealed class StoredLlmDocument
    {
        public string id { get; set; } = string.Empty;
        public DateTimeOffset analyzedAt { get; set; }
        public string? taxonomyVersion { get; set; }
        public LlmResponse response { get; set; } = null!;
    }

    private async Task<StoredAchievement[]?> GetStoredLlmAchievementsFromCosmos(string videoId)
    {
        // Connect to Cosmos emulator using environment setup values
        var connectionString = EnvironmentSetup.CosmosDbContainer
            .GetConnectionString()
            .Replace("https", "http");

        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        using var cosmos = new CosmosClient(connectionString, clientOptions);
        var db = cosmos.GetDatabase(EnvironmentSetup.CosmosDbDatabaseName);
        var container = db.GetContainer("video-analysis");

        try
        {
            var resp = await container.ReadItemAsync<StoredLlmDocument>(videoId, new PartitionKey(videoId));
            var dto = resp.Resource;

            var achievements = dto.response.VideoAnalysisResponse.Analysis.Achievements;
            if (achievements == null || achievements.Length == 0)
                return [];

            return achievements
                .Select(a => new StoredAchievement(a.Type, a.Tags, a.Details))
                .ToArray();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
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
