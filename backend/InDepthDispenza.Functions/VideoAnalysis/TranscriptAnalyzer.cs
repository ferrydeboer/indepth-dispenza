using InDepthDispenza.Functions.Interfaces;
using System.Text.Json;
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
            var promptText = await ComposePrompt(videoId);

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

    private async Task<string> ComposePrompt(string videoId)
    {
        var prompt = new Prompt();

        foreach (var composer in _promptComposers)
        {
            await composer.ComposeAsync(prompt, videoId);
        }

        var promptText = prompt.Build();
        return promptText;
    }

    /// <summary>
    /// Maps the typed LLM response into the domain VideoAnalysis.
    /// </summary>
    private VideoAnalysis ParseLlmResponse(string videoId, CommonLlmResponse common)
    {
        // Convert common assistant payload into domain DTO
        LlmVideoAnalysisResponseDto dto;
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (string.Equals(common.Assistant.ContentType, "application/json", StringComparison.OrdinalIgnoreCase)
            && common.Assistant.JsonContent is System.Text.Json.JsonElement json)
        {
            dto = JsonSerializer.Deserialize<LlmVideoAnalysisResponseDto>(json.GetRawText(), opts)
                  ?? new LlmVideoAnalysisResponseDto(new AnalysisDto(null, null, null, null, null), new ProposalsDto(null));
        }
        else
        {
            dto = JsonSerializer.Deserialize<LlmVideoAnalysisResponseDto>(common.Assistant.RawContent, opts)
                  ?? new LlmVideoAnalysisResponseDto(new AnalysisDto(null, null, null, null, null), new ProposalsDto(null));
        }

        var analysis = dto.Analysis;
        return new VideoAnalysis(
            Id: videoId,
            AnalyzedAt: DateTimeOffset.UtcNow,
            Achievements: dto.Analysis.Achievements ?? Array.Empty<Achievement>(),
            Timeframe: dto.Analysis.Timeframe,
            Practices: dto.Analysis.Practices ?? Array.Empty<string>(),
            ModelVersion: common.Call.Model,
            SentimentScore: analysis.SentimentScore ?? 0.0,
            ConfidenceScore: analysis.ConfidenceScore ?? 0.0,
            Proposals: dto.Proposals.Taxonomy
        );
    }
}

