using System.Text.Json;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Represents a taxonomy version document stored in Cosmos DB.
/// </summary>
/// <param name="Id">Version identifier (e.g., "v1.0", "v1.1")</param>
/// <param name="Taxonomy">The taxonomy JSON structure</param>
/// <param name="UpdatedAt">When this version was created</param>
/// <param name="Changes">Array of changes made in this version (for auditing)</param>
public record TaxonomyDocument(
    string Id,
    JsonDocument Taxonomy,
    DateTimeOffset UpdatedAt,
    string[] Changes
);
