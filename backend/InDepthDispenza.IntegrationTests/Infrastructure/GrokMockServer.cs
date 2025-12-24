using System.Text.Json;
using WireMock.Admin.Mappings;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

/// <summary>
/// Centralized WireMock stubs for Grok Chat Completions API used by integration tests.
/// </summary>
public class GrokMockServer(WireMockConfiguration wireMock)
{
    /// <summary>
    /// Registers a Grok chat completions response. Optionally provide achievements to include in the JSON content.
    /// If <paramref name="achievements"/> is null, an empty achievements array is returned.
    /// </summary>
    public async Task SetupAsync(object[]? achievements = null)
    {
        var grokResponse = new
        {
            id = "chatcmpl_default",
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
                        content = JsonSerializer.Serialize(new
                        {
                            analysis = new
                            {
                                achievements = achievements ?? Array.Empty<object>(),
                                timeframe = (object?)null,
                                practices = Array.Empty<string>(),
                                sentimentScore = 0.5,
                                confidenceScore = 0.5
                            },
                            proposals = new { taxonomy = Array.Empty<object>() }
                        })
                    },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 100, completion_tokens = 200, total_tokens = 300 }
        };

        var mappingModel = new MappingModel
        {
            Request = new RequestModel
            {
                Path = new PathModel
                {
                    Matchers = new[]
                    {
                        new MatcherModel { Name = "ExactMatcher", Pattern = "/v1/chat/completions" }
                    }
                },
                Methods = new[] { "POST" }
            },
            Response = new ResponseModel
            {
                StatusCode = 200,
                Headers = new Dictionary<string, object> { { "Content-Type", "application/json" } },
                Body = JsonSerializer.Serialize(grokResponse)
            }
        };

        var adminClient = wireMock.WireMockContainer.CreateWireMockAdminClient();
        await adminClient.PostMappingAsync(mappingModel);
    }
}
