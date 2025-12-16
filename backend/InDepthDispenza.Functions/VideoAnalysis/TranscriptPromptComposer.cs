using System.Text;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Composes the transcript portion of the LLM prompt.
/// Uses ITranscriptProvider to get the complete cached transcript document.
/// </summary>
public class TranscriptPromptComposer : IPromptComposer
{
    private readonly ILogger<TranscriptPromptComposer> _logger;
    private readonly ITranscriptProvider _transcriptProvider;

    public TranscriptPromptComposer(
        ILogger<TranscriptPromptComposer> logger,
        ITranscriptProvider transcriptProvider)
    {
        _logger = logger;
        _transcriptProvider = transcriptProvider;
    }

    public async Task ComposeAsync(Prompt prompt, string videoId)
    {
        // Get complete transcript document via provider (handles caching)
        var transcriptResult = await _transcriptProvider.GetTranscriptAsync(videoId, ["en"]);

        if (!transcriptResult.IsSuccess || transcriptResult.Data == null)
        {
            _logger.LogError(
                "Failed to fetch transcript for video {VideoId}: {Error}",
                videoId, transcriptResult.ErrorMessage);
            throw new InvalidOperationException(
                $"Cannot compose prompt: transcript not available for video {videoId}");
        }

        var doc = transcriptResult.Data;

        var content = new StringBuilder();
        content.AppendLine("# Video Transcript to Analyze");
        content.AppendLine();

        // Add video metadata
        content.AppendLine("**Video Metadata:**");
        content.AppendLine($"- Title: {doc.VideoTitle}");
        content.AppendLine($"- Language: {doc.Language}");
        content.AppendLine($"- Duration: {FormatDuration(doc.Duration)}");

        if (!string.IsNullOrWhiteSpace(doc.VideoDescription))
        {
            content.AppendLine($"- Description: {doc.VideoDescription}");
        }

        content.AppendLine();

        // Add transcript
        content.AppendLine("**Transcript:**");
        content.AppendLine();
        content.AppendLine("```");
        content.AppendLine(doc.Transcript ?? string.Empty);
        content.AppendLine("```");

        prompt.AddSegment(new PromptSegment(content.ToString(), Order: 20));
    }

    private static string FormatDuration(int seconds)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        if (timespan.TotalHours >= 1)
        {
            return $"{(int)timespan.TotalHours}h {timespan.Minutes}m {timespan.Seconds}s";
        }
        if (timespan.TotalMinutes >= 1)
        {
            return $"{timespan.Minutes}m {timespan.Seconds}s";
        }
        return $"{timespan.Seconds}s";
    }
}

