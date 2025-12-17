using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Prompting;

/// <summary>
/// Stub implementation of ILlmService for Step 2.
/// Returns a sample response for testing the analysis flow.
/// </summary>
public class StubLlmService : ILlmService
{
    private readonly ILogger<StubLlmService> _logger;

    public StubLlmService(ILogger<StubLlmService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a stubbed LLM response for testing.
    /// </summary>
    public Task<ServiceResult<CommonLlmResponse>> CallAsync(string prompt)
    {
        _logger.LogInformation(
            "StubLlmService called with prompt length: {Length}",
            prompt.Length);

        var dto = new LlmVideoAnalysisResponseDto(
            Analysis: new AnalysisDto(
                Achievements: new[]
                {
                    new Achievement(
                        Type: "healing",
                        Tags: new[] { "cancer", "cervical_cancer", "stage_four" },
                        Details: "Healed from stage 4 cervical cancer through consistent meditation practice")
                },
                Timeframe: new Timeframe("2 weeks", "6 months"),
                Practices: new[] { "meditation", "breath_work", "workshops" },
                SentimentScore: 0.85,
                ConfidenceScore: 0.9
            ),
            Proposals: new ProposalsDto(
                Taxonomy: null
            )
        );

        var raw = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        System.Text.Json.JsonElement? jsonElem = null;
        using (var doc = System.Text.Json.JsonDocument.Parse(raw))
        {
            jsonElem = doc.RootElement.Clone();
        }

        var call = new LlmCallInfo(
            Provider: "Stub",
            Model: "stub-model",
            DurationMs: 10,
            TokensPrompt: 21,
            TokensCompletion: 21,
            TokensTotal: 42,
            RequestId: null,
            CreatedAt: DateTimeOffset.UtcNow,
            FinishReason: "stop"
        );

        var assistant = new LlmAssistantPayload(
            RawContent: raw,
            ContentType: "application/json",
            JsonContent: jsonElem
        );

        var response = new CommonLlmResponse(call, assistant);

        return Task.FromResult(ServiceResult<CommonLlmResponse>.Success(response));
    }
}
