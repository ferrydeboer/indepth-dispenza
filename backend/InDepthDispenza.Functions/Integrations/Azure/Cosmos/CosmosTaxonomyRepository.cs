using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public class CosmosTaxonomyRepository : CosmosRepositoryBase, ITaxonomyRepository
{
    private readonly string _taxonomySeedFilePath;
    private bool _taxonomySeeded;
    private readonly SemaphoreSlim _seedingLock = new(1, 1);

    public CosmosTaxonomyRepository(
        ILogger<CosmosTaxonomyRepository> logger,
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options)
        : base(
            logger,
            cosmosClient,
            options.Value.DatabaseName ?? throw new InvalidOperationException("CosmosDb DatabaseName configuration is missing"),
            options.Value.TaxonomyVersionsContainer ?? throw new InvalidOperationException("CosmosDb TaxonomyVersionsContainer configuration is missing"))
    {
        _taxonomySeedFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "VideoAnalysis",
            "taxonomy-seed.json");
    }

    public async Task<ServiceResult<TaxonomyDocument?>> GetLatestTaxonomyAsync()
    {
        try
        {
            Logger.LogInformation("Retrieving latest taxonomy version from database");

            var container = await GetOrCreateContainerAsync();

            // Query for all documents, ordered by updatedAt descending, take 1
            var queryDefinition = new QueryDefinition(
                "SELECT TOP 1 * FROM c ORDER BY c.updatedAt DESC");

            var iterator = container.GetItemQueryIterator<CosmosTaxonomyDocument>(queryDefinition);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var cosmosDoc = response.FirstOrDefault();

                if (cosmosDoc != null)
                {
                    var document = cosmosDoc.ToTaxonomyDocument();
                    Logger.LogInformation("Successfully retrieved latest taxonomy version: {VersionId}", document.Id);
                    return ServiceResult<TaxonomyDocument?>.Success(document);
                }
            }

            Logger.LogInformation("No taxonomy versions found in database");

            // No taxonomy exists - seed it on first use (lazy initialization)
            await SeedTaxonomyIfNeededAsync();

            // Try again after seeding - need to create a new iterator
            var retryIterator = container.GetItemQueryIterator<CosmosTaxonomyDocument>(queryDefinition);
            if (retryIterator.HasMoreResults)
            {
                var retryResponse = await retryIterator.ReadNextAsync();
                var cosmosDoc = retryResponse.FirstOrDefault();

                if (cosmosDoc != null)
                {
                    var document = cosmosDoc.ToTaxonomyDocument();
                    Logger.LogInformation("Successfully retrieved taxonomy version after seeding: {VersionId}", document.Id);
                    return ServiceResult<TaxonomyDocument?>.Success(document);
                }
            }

            return ServiceResult<TaxonomyDocument?>.Success(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving latest taxonomy from database");
            return ServiceResult<TaxonomyDocument?>.Failure($"Failed to retrieve taxonomy: {ex.Message}", ex);
        }
    }

    public async Task<ServiceResult<TaxonomyDocument?>> GetTaxonomyByVersionAsync(string versionId)
    {
        try
        {
            Logger.LogInformation("Retrieving taxonomy version {VersionId} from database", versionId);

            var container = await GetOrCreateContainerAsync();
            var response = await container.ReadItemAsync<CosmosTaxonomyDocument>(
                versionId,
                new PartitionKey(versionId));

            var document = response.Resource.ToTaxonomyDocument();

            Logger.LogInformation("Successfully retrieved taxonomy version {VersionId}", versionId);
            return ServiceResult<TaxonomyDocument?>.Success(document);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Logger.LogInformation("Taxonomy version {VersionId} not found", versionId);
            return ServiceResult<TaxonomyDocument?>.Success(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving taxonomy version {VersionId} from database", versionId);
            return ServiceResult<TaxonomyDocument?>.Failure($"Failed to retrieve taxonomy: {ex.Message}", ex);
        }
    }

    public async Task<ServiceResult> SaveTaxonomyAsync(TaxonomyDocument document)
    {
        try
        {
            Logger.LogInformation("Saving taxonomy version {VersionId} to database", document.Id);

            var cosmosDocument = CosmosTaxonomyDocument.FromTaxonomyDocument(document);
            var container = await GetOrCreateContainerAsync();

            await container.UpsertItemAsync(
                cosmosDocument,
                new PartitionKey(cosmosDocument.id));

            Logger.LogInformation("Successfully saved taxonomy version {VersionId}", document.Id);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving taxonomy version {VersionId} to database", document.Id);
            return ServiceResult.Failure($"Failed to save taxonomy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Seeds the initial taxonomy from file if it doesn't exist yet.
    /// Uses semaphore to ensure only one thread seeds the taxonomy.
    /// </summary>
    private async Task SeedTaxonomyIfNeededAsync()
    {
        if (_taxonomySeeded)
            return;

        await _seedingLock.WaitAsync();
        try
        {
            if (_taxonomySeeded)
                return;

            Logger.LogInformation("Checking if taxonomy needs seeding");

            if (!File.Exists(_taxonomySeedFilePath))
            {
                Logger.LogWarning("Taxonomy seed file not found at {FilePath}", _taxonomySeedFilePath);
                _taxonomySeeded = true; // Don't try again
                return;
            }

            Logger.LogInformation("Seeding initial taxonomy from {FilePath}", _taxonomySeedFilePath);

            var jsonString = await File.ReadAllTextAsync(_taxonomySeedFilePath);
            var taxonomyJson = JsonDocument.Parse(jsonString);

            var initialTaxonomy = new TaxonomyDocument(
                Id: "v1.0",
                Taxonomy: taxonomyJson,
                UpdatedAt: DateTimeOffset.UtcNow,
                Changes: new[] { "Initial taxonomy version" }
            );

            var saveResult = await SaveTaxonomyAsync(initialTaxonomy);

            if (saveResult.IsSuccess)
            {
                Logger.LogInformation("Successfully seeded initial taxonomy version v1.0");
                _taxonomySeeded = true;
            }
            else
            {
                Logger.LogError("Failed to seed taxonomy: {Error}", saveResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error seeding taxonomy from file");
        }
        finally
        {
            _seedingLock.Release();
        }
    }

    // Internal Cosmos document representation with lowercase 'id' for Cosmos DB
    private sealed class CosmosTaxonomyDocument
    {
        public string id { get; set; } = string.Empty;
        public string taxonomy { get; set; }
        public DateTimeOffset updatedAt { get; set; }
        public string[] changes { get; set; } = Array.Empty<string>();
        public string? proposedFromVideoId { get; set; }

        public static CosmosTaxonomyDocument FromTaxonomyDocument(TaxonomyDocument document)
        {
            var taxonomy = document.Taxonomy.RootElement.GetRawText();
            
            return new CosmosTaxonomyDocument
            {
                id = document.Id,
                taxonomy = taxonomy,
                updatedAt = document.UpdatedAt,
                changes = document.Changes,
                proposedFromVideoId = document.ProposedFromVideoId
            };
        }

        public TaxonomyDocument ToTaxonomyDocument()
        {
            return new TaxonomyDocument(
                Id: id,
                Taxonomy: JsonDocument.Parse(taxonomy),
                UpdatedAt: updatedAt,
                Changes: changes,
                ProposedFromVideoId: proposedFromVideoId
            );
        }
    }
}
