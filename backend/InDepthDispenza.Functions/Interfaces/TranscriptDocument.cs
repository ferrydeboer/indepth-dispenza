namespace InDepthDispenza.Functions.Interfaces;

public record TranscriptDocument(
    string Id,
    string? Transcript,
    string Language,
    DateTimeOffset FetchedAt,
    string VideoTitle,
    string Duration);
