namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Provides access to video transcripts from various sources with caching.
/// Returns complete TranscriptDocument with all metadata and segments.
/// </summary>
public interface ITranscriptProvider
{
    /// <summary>
    /// Gets the complete transcript document for a video, including metadata and segments.
    /// Handles caching transparently.
    /// </summary>
    /// <param name="videoId">The video ID</param>
    /// <param name="preferredLanguages">Preferred languages in order of preference</param>
    /// <returns>Service result containing the complete transcript document</returns>
    Task<ServiceResult<TranscriptDocument>> GetTranscriptAsync(string videoId, string[] preferredLanguages);
}

/// <summary>
/// Represents transcript data with text, language, and timing information.
/// Common denominator between YouTube Transcript IO API and YouTube Data API captions.
/// </summary>
public record TranscriptData(
    string Text,
    string Language,
    TranscriptSegment[] Segments,
    TranscriptMetadata? Metadata = null
);

/// <summary>
/// Individual segment of transcript with timing information.
/// Compatible with both YouTube Transcript IO (start/dur) and standard caption formats (start/end).
/// </summary>
public record TranscriptSegment(
    decimal StartSeconds,
    decimal DurationSeconds,
    string Text
)
{
    /// <summary>
    /// End time in seconds (calculated from start + duration)
    /// </summary>
    public decimal EndSeconds => StartSeconds + DurationSeconds;
}

/// <summary>
/// Optional metadata about the video transcript.
/// </summary>
public record TranscriptMetadata(
    string? Title = null,
    string? Description = null,
    string? ChannelName = null,
    string? Category = null,
    int? LengthSeconds = null,
    DateTimeOffset? PublishDate = null
);
