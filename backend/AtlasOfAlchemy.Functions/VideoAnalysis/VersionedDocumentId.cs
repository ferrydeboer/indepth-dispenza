using System.Globalization;

namespace AtlasOfAlchemy.Functions.VideoAnalysis;

/// <summary>
/// Represents a versioned document ID combining a video ID with a timestamp.
/// Format: {videoId}_{ISO8601} (e.g., dQw4w9WgXcQ_20260401T120000Z)
/// </summary>
public readonly record struct VersionedDocumentId(string VideoId, DateTimeOffset Timestamp)
{
    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const char Separator = '_';

    /// <summary>
    /// Gets the formatted document ID value.
    /// </summary>
    public string Value => $"{VideoId}{Separator}{Timestamp.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Creates a new VersionedDocumentId from a video ID and timestamp.
    /// </summary>
    public static VersionedDocumentId Create(string videoId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        return new VersionedDocumentId(videoId, timestamp);
    }

    /// <summary>
    /// Attempts to parse a document ID string into a VersionedDocumentId.
    /// </summary>
    public static bool TryParse(string documentId, out VersionedDocumentId result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return false;
        }

        var lastSeparatorIndex = documentId.LastIndexOf(Separator);
        if (lastSeparatorIndex <= 0 || lastSeparatorIndex == documentId.Length - 1)
        {
            return false;
        }

        var videoId = documentId[..lastSeparatorIndex];
        var timestampPart = documentId[(lastSeparatorIndex + 1)..];

        if (!DateTime.TryParseExact(timestampPart, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return false;
        }

        result = new VersionedDocumentId(videoId, new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc), TimeSpan.Zero));
        return true;
    }

    /// <summary>
    /// Extracts the video ID from a document ID, handling both legacy (plain video ID) and versioned formats.
    /// </summary>
    public static string ExtractVideoId(string documentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        if (TryParse(documentId, out var versionedId))
        {
            return versionedId.VideoId;
        }

        // Legacy format: document ID is the video ID itself
        return documentId;
    }
}
