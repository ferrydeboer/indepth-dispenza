using InDepthDispenza.Functions.VideoAnalysis.Interfaces;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Document for storing the full video analysis enriched with metadata.
/// </summary>
/// <param name="Id">Video ID</param>
/// <param name="AnalyzedAt">When the analysis was performed</param>
/// <param name="ModelVersion">LLM model used</param>
/// <param name="Achievements">Extracted achievements</param>
/// <param name="Timeframe">Timeframe information</param>
/// <param name="Practices">Practices mentioned</param>
/// <param name="SentimentScore">Sentiment score</param>
/// <param name="ConfidenceScore">Confidence score</param>
/// <param name="Proposals">Optional taxonomy proposals</param>
/// <param name="TaxonomyVersion">Taxonomy version that was applied/created during analysis flow</param>
public sealed record VideoAnalysisDocument(
    string Id,
    DateTimeOffset AnalyzedAt,
    string ModelVersion,
    Achievement[] Achievements,
    Timeframe? Timeframe,
    string[] Practices,
    double SentimentScore,
    double ConfidenceScore,
    TaxonomyProposal[]? Proposals,
    string? TaxonomyVersion
);
