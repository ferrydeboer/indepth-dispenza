using Newtonsoft.Json;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Root taxonomy specification with explicit taxonomy property.
/// Includes a version identifier used as a stable taxonomy version.
/// </summary>
public class TaxonomySpecification
{
    [JsonProperty("id")]
    [JsonConverter(typeof(NewtonsoftTaxonomyVersionConverter))]
    [global::System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonTaxonomyVersionConverter))]
    public TaxonomyVersion Version { get; set; } = new(1, 0);

    /// <summary>
    /// Top-level taxonomy map. Keys are domain names, values are groups of categories.
    /// </summary>
    [JsonProperty("taxonomy")]
    public Dictionary<string, AchievementTypeGroup> Taxonomy { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    
    [JsonProperty("rules")]
    public Dictionary<string, string> Rules { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Array of change notes for auditing.
    /// </summary>
    [JsonIgnore]
    public string[] Changes { get; set; } = Array.Empty<string>();
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
    [JsonProperty("subcategories")]
    public List<string>? Subcategories { get; init; }

    [JsonProperty("attributes")]
    public List<string>? Attributes { get; init; }
}
