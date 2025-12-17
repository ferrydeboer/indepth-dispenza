using System.Text.Json.Serialization;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Strong-typed taxonomy document used by the repository and services.
/// Inherits the taxonomy map and the Version from TaxonomySpecification and adds Cosmos-only metadata.
/// </summary>
public sealed class TaxonomyDocument : TaxonomySpecification
{
    /// <summary>
    /// When this version was created/saved.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Array of change notes for auditing.
    /// </summary>
    [JsonIgnore]
    public string[] Changes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional reference to the videoId that proposed this version.
    /// </summary>
    [JsonIgnore]
    public string? ProposedFromVideoId { get; set; }
}