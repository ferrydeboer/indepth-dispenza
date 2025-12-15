using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Main business logic for analyzing video transcripts.
/// Orchestrates fetching transcripts and passing them to LLM for analysis.
/// Contains all application logic - Functions layer only handles HTTP concerns.
/// </summary>
public class TranscriptAnalyzer
{
    private readonly ILogger<TranscriptAnalyzer> _logger;
    private readonly ITranscriptProvider _transcriptProvider;
    private readonly VideoAnalysisOptions _options;

    public TranscriptAnalyzer(
        ILogger<TranscriptAnalyzer> logger,
        ITranscriptProvider transcriptProvider,
        IOptions<VideoAnalysisOptions> options)
    {
        _logger = logger;
        _transcriptProvider = transcriptProvider;
        _options = options.Value;
    }

    /// <summary>
    /// Analyzes a video by ID. Handles all business logic including:
    /// - Creating VideoInfo (placeholder metadata)
    /// - Determining preferred languages
    /// - Fetching transcript with caching
    /// - Preparing result for LLM analysis
    /// </summary>
    public async Task<ServiceResult<TranscriptAnalysisResult>> AnalyzeVideoAsync(string videoId)
    {
        try
        {
            _logger.LogInformation("Starting analysis for video {VideoId}", videoId);

            // Business logic: Create VideoInfo (in future, this might fetch from YouTube API)
            var videoInfo = new VideoInfo(
                VideoId: videoId,
                Title: "Placeholder", // TODO: Fetch actual metadata
                Description: string.Empty,
                ChannelTitle: string.Empty,
                PublishedAt: DateTimeOffset.UtcNow,
                ThumbnailUrl: string.Empty
            );

            // Business logic: Determine preferred languages from configuration
            var preferredLanguages = _options.PreferredLanguages ?? ["en"];

            // Fetch transcript (caching is transparent)
            var transcriptResult = await _transcriptProvider.GetTranscriptAsync(
                videoId,
                preferredLanguages);

            if (!transcriptResult.IsSuccess || transcriptResult.Data == null)
            {
                return ServiceResult<TranscriptAnalysisResult>.Failure(
                    transcriptResult.ErrorMessage ?? "Failed to fetch transcript",
                    transcriptResult.Exception);
            }

            var transcript = transcriptResult.Data;

            if (string.IsNullOrEmpty(transcript.Text))
            {
                _logger.LogWarning("No transcript available for video {VideoId}", videoId);
                return ServiceResult<TranscriptAnalysisResult>.Success(
                    new TranscriptAnalysisResult(
                        VideoId: videoId,
                        Title: videoInfo.Title,
                        TranscriptText: string.Empty,
                        Language: "unknown",
                        CharacterCount: 0,
                        WordCount: 0,
                        IsEmpty: true,
                        ReadyForLlm: false
                    ));
            }

            // Prepare transcript for LLM analysis
            var wordCount = CountWords(transcript.Text);
            var charCount = transcript.Text.Length;

            _logger.LogInformation(
                "Transcript retrieved for video {VideoId}. Language: {Language}, Words: {WordCount}, Characters: {CharCount}",
                videoId, transcript.Language, wordCount, charCount);

            // TODO: Pass transcript to LLM for analysis
            // This would be the next step in the pipeline

            var result = new TranscriptAnalysisResult(
                VideoId: videoId,
                Title: videoInfo.Title,
                TranscriptText: transcript.Text,
                Language: transcript.Language,
                CharacterCount: charCount,
                WordCount: wordCount,
                IsEmpty: false,
                ReadyForLlm: true
            );

            _logger.LogInformation("Video {VideoId} is ready for LLM analysis", videoId);

            return ServiceResult<TranscriptAnalysisResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing video {VideoId}", videoId);
            return ServiceResult<TranscriptAnalysisResult>.Failure(
                $"Failed to analyze video: {ex.Message}",
                ex);
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

/// <summary>
/// Configuration options for video analysis.
/// </summary>
public class VideoAnalysisOptions
{
    public string[]? PreferredLanguages { get; set; }
}

/// <summary>
/// Result of transcript analysis, ready for LLM processing.
/// </summary>
public record TranscriptAnalysisResult(
    string VideoId,
    string Title,
    string TranscriptText,
    string Language,
    int CharacterCount,
    int WordCount,
    bool IsEmpty,
    bool ReadyForLlm
);
