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
    private readonly IVideoAnalysisRepository _videoAnalysisRepository;
    private readonly IEnumerable<IPromptComposer> _promptComposers;
    private readonly IEnumerable<IVideoAnalyzedHandler> _videosAnalyzedHandlers;

    public TranscriptAnalyzer(
        ILogger<TranscriptAnalyzer> logger,
        ILlmService llmService,
        IEnumerable<IPromptComposer> promptComposers,
        IEnumerable<IVideoAnalyzedHandler> videosAnalyzedHandlers,
        IVideoAnalysisRepository videoAnalysisRepository)
    {
        _logger = logger;
        _llmService = llmService;
        _promptComposers = promptComposers;
        _videosAnalyzedHandlers = videosAnalyzedHandlers;
        _videoAnalysisRepository = videoAnalysisRepository;
    }

    /// <summary>
    /// Analyzes a transcript to extract structured healing journey data.
    /// </summary>
    public async Task<ServiceResult<VideoAnalysis>> AnalyzeTranscriptAsync(string videoId)
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
        
        // Build PromptExecutionInfoDto from LlmCallInfo ensuring parity
        var call = llmResult.Data.Call;
        var promptInfo = new PromptExecutionInfoDto(
            Provider: call.Provider,
            Model: call.Model,
            DurationMs: call.DurationMs,
            TokensPrompt: call.TokensPrompt,
            TokensCompletion: call.TokensCompletion,
            TokensTotal: call.TokensTotal,
            RequestId: call.RequestId,
            CreatedAt: call.CreatedAt,
            FinishReason: call.FinishReason
        );

        // Step 3: Parse LLM response into VideoAnalysis object and attach prompt info into DTO for storage later
        var (analysis, responseDto) = ParseLlmResponse(videoId, llmResult.Data, promptInfo);
        _lastLlmResponse = responseDto; // cache for storage
        _lastVideoId = videoId;
        _lastAnalyzedAt = analysis.AnalyzedAt;

        // Post-processing handlers (ordered); isolate exceptions per handler
        _lastTaxonomyVersion = null;
        var context = new VideosAnalyzedContext(
            videoId,
            _logger);

        await OnVideoAnalyzed(responseDto, context);

        // Persist full LLM response + metadata
        var persist = await PersistLastAnalysisAsync(null);
        if (!persist.IsSuccess)
        {
            _logger.LogError("Failed to persist video analysis for {VideoId}: {Error}", videoId, persist.ErrorMessage);
        }

        _logger.LogInformation(
            "Completed transcript analysis for video {VideoId}. Found {AchievementCount} achievements",
            videoId, analysis.Achievements.Length);

        return ServiceResult<VideoAnalysis>.Success(analysis);
    }

    private async Task OnVideoAnalyzed(LlmResponse response, VideosAnalyzedContext context)
    {
        foreach (var handler in _videosAnalyzedHandlers)
        {
            try
            {
                await handler.HandleAsync(response, context);
            }
            catch (Exception handlerEx)
            {
                _logger.LogError(handlerEx, "Post-processing handler {Handler} failed for video {VideoId}", handler.GetType().Name, context.VideoId);
            }
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
    private (VideoAnalysis Analysis, LlmResponse Dto) ParseLlmResponse(string videoId, CommonLlmResponse common, PromptExecutionInfoDto promptInfo)
    {
        // Convert common assistant payload into domain DTO
        LlmVideoAnalysisResponseDto dto;
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (string.Equals(common.Assistant.ContentType, "application/json", StringComparison.OrdinalIgnoreCase)
            && common.Assistant.JsonContent is JsonElement json)
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
        var videoAnalysis = new VideoAnalysis(
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
        var response = new LlmResponse(promptInfo, dto);
        return (videoAnalysis, response);
    }

    // State of the last analysis for persistence
    private LlmResponse? _lastLlmResponse;
    private string? _lastVideoId;
    private DateTimeOffset _lastAnalyzedAt;
    private string? _lastTaxonomyVersion;

    public async Task<ServiceResult> PersistLastAnalysisAsync(string? taxonomyVersion)
    {
        try
        {
            if (_lastLlmResponse == null || string.IsNullOrEmpty(_lastVideoId))
            {
                return ServiceResult.Failure("No analysis available to persist");
            }

            var store = await _videoAnalysisRepository.SaveFullLlmResponseAsync(
                id: _lastVideoId!,
                analyzedAt: _lastAnalyzedAt,
                taxonomyVersion: taxonomyVersion ?? _lastTaxonomyVersion,
                llm: _lastLlmResponse);

            return store;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist last analysis for {VideoId}", _lastVideoId);
            return ServiceResult.Failure($"Failed to persist last analysis: {ex.Message}", ex);
        }
    }

    public string? GetLastTaxonomyVersion() => _lastTaxonomyVersion;
}

