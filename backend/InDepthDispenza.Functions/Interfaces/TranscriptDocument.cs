namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Document storing all transcript information for caching and analysis operations.
/// Stores segments with timing information, from which full text can be derived.
/// </summary>
/// <param name="Id">Video ID</param>
/// <param name="Segments">Array of transcript segments with timing information</param>
/// <param name="Language">ISO-based language code of the transcript</param>
/// <param name="FetchedAt">Timestamp when transcript was fetched</param>
/// <param name="VideoTitle">Title of the video</param>
/// <param name="VideoDescription">Description from the video</param>
/// <param name="Duration">Video length in seconds</param>
public record TranscriptDocument(
    string Id,
    TranscriptSegment[] Segments,
    string Language,
    DateTimeOffset FetchedAt,
    string VideoTitle,
    string VideoDescription,
    int Duration)
{
    /// <summary>
    /// Gets the full transcript text by concatenating all segments.
    /// This is derived on-demand rather than stored to minimize cache size.
    /// </summary>
    public string GetFullText()
    {
        if (Segments == null || Segments.Length == 0)
            return string.Empty;

        return string.Join(" ", Segments.Select(s => s.Text));
    }

    /// <summary>
    /// Legacy property for backwards compatibility.
    /// Use GetFullText() for explicit text retrieval.
    /// </summary>
    public string? Transcript => GetFullText();
};
