using System.Text.Json.Serialization;

namespace InDepthDispenza.Functions.VideoAnalysis.Prompting;

/// <summary>
/// Root model to capture the structure of the taxonomy seed file.
/// Focuses on the <c>taxonomy</c> property which contains hierarchical domains and categories.
/// </summary>
public sealed class TaxonomySeed
{
    /// <summary>
    /// Top-level taxonomy map.
    /// Keys are domain names like <c>healing</c>, <c>manifestation</c>, <c>transformation</c>, <c>other</c>.
    /// Values are groups of categories under each domain.
    /// </summary>
    [JsonPropertyName("taxonomy")]
    public Dictionary<string, AchievementTypeGroup> Taxonomy { get; init; } = new();
}

/// <summary>
/// Represents a group of achievement types/categories under a top-level domain.
/// The JSON is an object with category names as keys and category nodes as values.
/// Using a dictionary preserves flexibility for arbitrary category names and supports empty objects (e.g., <c>other</c>).
/// </summary>
public sealed class AchievementTypeGroup : Dictionary<string, CategoryNode>
{
}

/// <summary>
/// Represents a concrete category node with optional subcategories and attributes arrays.
/// </summary>
public sealed class CategoryNode
{
    [JsonPropertyName("subcategories")]
    public List<string>? Subcategories { get; init; }

    [JsonPropertyName("attributes")]
    public List<string>? Attributes { get; init; }
}
