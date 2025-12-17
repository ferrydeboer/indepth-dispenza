using System.Text.Json.Serialization;
using InDepthDispenza.Functions.VideoAnalysis.Prompting;
using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

/// <summary>
/// DTO for deserializing LLM response JSON.
/// </summary>
public sealed record LlmResponse(
    PromptExecutionInfoDto PromptInfo,
    LlmVideoAnalysisResponseDto VideoAnalysisResponse
); 
    
public sealed record LlmVideoAnalysisResponseDto(
    AnalysisDto Analysis,
    ProposalsDto Proposals
);
    
public sealed record AnalysisDto(
    Achievement[]? Achievements,
    Timeframe? Timeframe,
    string[]? Practices,
    double? SentimentScore,
    double? ConfidenceScore
);

/// <summary>
/// Meta data about the request for analysis purposes.
/// </summary>
/// <param name="ModelVersion">The version of the model used.</param>
/// <param name="TokensUsed">The amount of tokens used in the request.</param>
/// <param name="Duration">The time it took to execute the request.</param>
public sealed record PromptExecutionInfoDto(
    string ModelVersion,
    int TokensUsed,
    int Duration
);

public sealed record ProposalsDto(
    TaxonomyProposal[]? Taxonomy
);

/// <summary>
/// Represents a taxonomy addition proposed by the LLM.
/// </summary>
/// <param name="Suggestion">The new Taxonomy hierarchy.</param>
/// <param name="Justification">Explanation for why this addition is needed</param>
[JsonConverter(typeof(TaxonomyProposalJsonConverter))]
public class TaxonomyProposal
{
    public TaxonomyProposal(string achievementCategory, AchievementTypeGroup group, string justification)
    {
        AchievementCategory = achievementCategory;
        Group = group;
        Justification = justification;
    }

    /// <summary>
    /// The parent achievement domain/category name that should appear as a dynamic property in JSON.
    /// </summary>
    public string AchievementCategory { get; init; }

    /// <summary>
    /// The proposed group structure under the parent category (e.g., new nodes and their children).
    /// </summary>
    public AchievementTypeGroup Group { get; init; }

    /// <summary>
    /// Explanation for why this addition is needed.
    /// Serialized next to the dynamic property as "justification".
    /// </summary>
    [JsonPropertyName("justification")]
    public string Justification { get; init; }
}

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