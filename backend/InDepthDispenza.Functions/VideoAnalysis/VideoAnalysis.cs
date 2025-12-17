using InDepthDispenza.Functions.VideoAnalysis.Prompting;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Represents the structured analysis of a video transcript.
/// Follows the schema defined in Story 2 acceptance criteria.
/// </summary>
/// <param name="Id">Video ID</param>
/// <param name="AnalyzedAt">Timestamp when analysis was performed</param>
/// <param name="ModelVersion">LLM model used (e.g., "gpt-4o-mini")</param>
/// <param name="PromptVersion">Version of the prompt template used</param>
/// <param name="TaxonomyVersion">Reference to the taxonomy version used</param>
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
    string PromptVersion,
    string TaxonomyVersion,
    Achievement[] Achievements,
    Timeframe? Timeframe,
    string[] Practices,
    double SentimentScore,
    double ConfidenceScore,
    TaxonomyProposal[]? Proposals
);

/// <summary>
/// Represents a single achievement (healing, manifestation, transformation, etc.).
/// </summary>
/// <param name="Type">Type of achievement: healing, manifestation, transformation, other</param>
/// <param name="Tags">Array of snake_case tags constrained by taxonomy</param>
/// <param name="Details">Optional brief narrative description</param>
public record Achievement(
    string Type,
    string[] Tags,
    string? Details
);

/// <summary>
/// Represents timeframe information for healing or manifestation.
/// </summary>
/// <param name="NoticeEffects">Time string when effects were first noticed (e.g., "2 weeks")</param>
/// <param name="FullHealing">Time string for full healing/manifestation (e.g., "6 months")</param>
public record Timeframe(
    string? NoticeEffects,
    string? FullHealing
);

/// <summary>
/// Represents a taxonomy addition proposed by the LLM.
/// </summary>
/// <param name="Suggestion">The new Taxonomy hierarchy.</param>
/// <param name="Justification">Explanation for why this addition is needed</param>
public record TaxonomyProposal(
    AchievementTypeGroup Suggestion,
    string Justification
);
