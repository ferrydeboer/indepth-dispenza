using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Analyzes video transcripts using LLM with taxonomy-constrained extraction.
/// Orchestrates the prompt composition pipeline and LLM call.
/// </summary>
public class TranscriptAnalyzer : ITranscriptAnalyzer
{
    private readonly ILogger<TranscriptAnalyzer> _logger;
    private readonly ILlmService _llmService;
    private readonly IEnumerable<IPromptComposer> _promptComposers;

    public TranscriptAnalyzer(
        ILogger<TranscriptAnalyzer> logger,
        ILlmService llmService,
        IEnumerable<IPromptComposer> promptComposers)
    {
        _logger = logger;
        _llmService = llmService;
        _promptComposers = promptComposers;
    }

    /// <summary>
    /// Analyzes a transcript to extract structured healing journey data.
    /// </summary>
    public async Task<ServiceResult<VideoAnalysis>> AnalyzeTranscriptAsync(string videoId)
    {
        try
        {
            _logger.LogInformation("Starting transcript analysis for video {VideoId}", videoId);

            // Step 1: Compose prompt using the pipeline
            var prompt = new Prompt();

            foreach (var composer in _promptComposers)
            {
                await composer.ComposeAsync(prompt, videoId);
            }

            var promptText = prompt.Build();

            _logger.LogInformation(
                "Composed prompt for video {VideoId}. Prompt length: {PromptLength} characters",
                videoId, promptText.Length);

            // Step 2: Call LLM service with composed prompt
            var llmResult = await _llmService.CallAsync(promptText);

            if (!llmResult.IsSuccess || llmResult.Data == null)
            {
                return ServiceResult<VideoAnalysis>.Failure(
                    $"LLM analysis failed: {llmResult.ErrorMessage}",
                    llmResult.Exception);
            }

            // Step 3: Parse LLM response into VideoAnalysis object
            var analysis = ParseLlmResponse(videoId, llmResult.Data);

            _logger.LogInformation(
                "Completed transcript analysis for video {VideoId}. Found {AchievementCount} achievements",
                videoId, analysis.Achievements.Length);

            return ServiceResult<VideoAnalysis>.Success(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing transcript for video {VideoId}", videoId);
            return ServiceResult<VideoAnalysis>.Failure(
                $"Failed to analyze transcript: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Parses the LLM JSON response into a VideoAnalysis object.
    /// Uses System.Text.Json deserialization for simplicity.
    /// </summary>
    private VideoAnalysis ParseLlmResponse(string videoId, JsonDocument llmResponse)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Deserialize to a DTO that matches the LLM response structure
        var dto = JsonSerializer.Deserialize<LlmResponseDto>(llmResponse, options);

        if (dto == null)
        {
            throw new InvalidOperationException("Failed to deserialize LLM response");
        }

        return new VideoAnalysis(
            Id: videoId,
            AnalyzedAt: DateTimeOffset.UtcNow,
            ModelVersion: dto.ModelVersion ?? "gpt-4o-mini",
            PromptVersion: dto.PromptVersion ?? "v1.0",
            TaxonomyVersion: "v1.0", // TODO: Get from TaxonomyPromptComposer
            Achievements: dto.Achievements ?? Array.Empty<Achievement>(),
            Timeframe: dto.Timeframe,
            Practices: dto.Practices ?? Array.Empty<string>(),
            SentimentScore: dto.SentimentScore,
            ConfidenceScore: dto.ConfidenceScore,
            Proposals: dto.Proposals
        );
    }

    /// <summary>
    /// DTO for deserializing LLM response JSON.
    /// </summary>
    private sealed record LlmResponseDto(
        string? ModelVersion,
        string? PromptVersion,
        Achievement[]? Achievements,
        Timeframe? Timeframe,
        string[]? Practices,
        double SentimentScore,
        double ConfidenceScore,
        TaxonomyProposal[]? Proposals
    );
}
