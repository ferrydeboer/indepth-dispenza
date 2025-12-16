namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Intent of this document is to store all information that can be used for analysis & caching operations. 
/// </summary>
/// <remarks>At this point it does not store the timing of the captions. This might prove valuable later in segmenting
/// the video for browsing</remarks>
/// <param name="Id"></param>
/// <param name="Transcript">The actual transcript.</param>
/// <param name="Language">ISO based language of the transcript. Might need comma separation since some videos contain
/// tolks translating</param>
/// <param name="FetchedAt">The moment the transcript was fetched. Can be used for eviction of certain caches to
/// accomodate for changes.</param>
/// <param name="VideoTitle">The title of video.</param>
/// <param name="VideoDescription">The description as listed under the video.</param>
/// <param name="Duration">The length of the video in seconds</param>
public record TranscriptDocument(
    string Id,
    string? Transcript,
    string Language,
    DateTimeOffset FetchedAt,
    string VideoTitle,
    string VideoDescription,
    int Duration);
