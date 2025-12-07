namespace InDepthDispenza.Functions.Interfaces;

public record VideoInfo(
    string VideoId,
    string Title,
    string Description,
    string ChannelTitle,
    DateTimeOffset PublishedAt,
    string ThumbnailUrl,
    TimeSpan? Duration = null,
    long? ViewCount = null);