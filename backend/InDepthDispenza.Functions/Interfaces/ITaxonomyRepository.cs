namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Repository for managing taxonomy versions in Cosmos DB.
/// </summary>
public interface ITaxonomyRepository
{
    /// <summary>
    /// Retrieves the latest taxonomy version from the database.
    /// </summary>
    /// <returns>The latest taxonomy document, or null if none exists</returns>
    Task<ServiceResult<TaxonomyDocument?>> GetLatestTaxonomyAsync();

    /// <summary>
    /// Retrieves a specific taxonomy version by ID.
    /// </summary>
    /// <param name="versionId">The version ID (e.g., "v1.0")</param>
    /// <returns>The taxonomy document for the specified version</returns>
    Task<ServiceResult<TaxonomyDocument?>> GetTaxonomyByVersionAsync(string versionId);

    /// <summary>
    /// Saves a new taxonomy version to the database.
    /// </summary>
    /// <param name="document">The taxonomy document to save</param>
    Task<ServiceResult> SaveTaxonomyAsync(TaxonomyDocument document);
}
