using System.Text.Json.Serialization;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Root taxonomy specification with explicit taxonomy property.
/// Includes a version identifier used as a stable taxonomy version.
/// </summary>
public class TaxonomySpecification
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "v1.0";

    /// <summary>
    /// Top-level taxonomy map. Keys are domain names, values are groups of categories.
    /// </summary>
    [JsonPropertyName("taxonomy")]
    public Dictionary<string, AchievementTypeGroup> Taxonomy { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a group of achievement types/categories under a top-level domain.
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
