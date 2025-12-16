using System.Text.Json;
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
    public Task<ServiceResult<JsonDocument>> CallAsync(string prompt)
    {
        _logger.LogInformation(
            "StubLlmService called with prompt length: {Length}",
            prompt.Length);

        // Return a sample response matching the VideoAnalysis schema
        var stubResponse = JsonDocument.Parse("""
            {
              "modelVersion": "gpt-4o-mini",
              "achievements": [
                {
                  "type": "healing",
                  "tags": ["cancer", "cervical_cancer", "stage_four"],
                  "details": "Healed from stage 4 cervical cancer through consistent meditation practice"
                }
              ],
              "timeframe": {
                "noticeEffects": "2 weeks",
                "fullHealing": "6 months"
              },
              "practices": ["meditation", "breath_work", "workshops"],
              "sentimentScore": 0.85,
              "confidenceScore": 0.9,
              "proposals": [
                {
                  "newTag": "stage_four_remission",
                  "parent": "cancer",
                  "justification": "Many testimonials describe full remission from stage 4 cancers"
                }
              ]
            }
            """);

        return Task.FromResult(ServiceResult<JsonDocument>.Success(stubResponse));
    }
}
