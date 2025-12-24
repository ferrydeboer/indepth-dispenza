using InDepthDispenza.Functions.VideoAnalysis.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Represents the structured analysis of a video transcript.
/// Follows the schema defined in Story 2 acceptance criteria.
/// </summary>
/// <param name="Id">Video ID</param>
/// <param name="AnalyzedAt">Timestamp when analysis was performed</param>
/// <param name="ModelVersion">LLM model used (e.g., "gpt-4o-mini")</param>
/// <param name="Achievements">Array of achievements extracted from the video</param>
/// <param name="Timeframe">Timeframe information for healing/manifestation</param>
/// <param name="Practices">Array of practices/techniques used</param>
/// <param name="SentimentScore">Sentiment score between 0-1</param>
/// <param name="ConfidenceScore">Confidence score between 0-1</param>
/// <param name="Proposals">Optional array of taxonomy additions proposed by LLM</param>
public record VideoAnalysis(
    string Id,
    DateTimeOffset AnalyzedAt,
    string ModelVersion,
    Achievement[] Achievements,
    Timeframe? Timeframe,
    string[] Practices,
    double SentimentScore,
    double ConfidenceScore,
    TaxonomyProposal[]? Proposals
)
{
    public string VideoId => Id;
}

