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
            "Taxonomy",
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
                    Logger.LogInformation("Successfully retrieved latest taxonomy version: {VersionId}", document.Version);
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
                    Logger.LogInformation("Successfully retrieved taxonomy version after seeding: {VersionId}", document.Version);
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
            Logger.LogInformation("Saving taxonomy version {VersionId} to database", document.Version);

            var cosmosDocument = CosmosTaxonomyDocument.FromTaxonomyDocument(document);
            var container = await GetOrCreateContainerAsync();

            await container.UpsertItemAsync(
                cosmosDocument,
                new PartitionKey(cosmosDocument.id));

            Logger.LogInformation("Successfully saved taxonomy version {VersionId}", document.Version);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving taxonomy version {VersionId} to database", document.Version);
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
            // Seed can be either flat map or wrapped under {"taxonomy": ...} and may contain a top-level "version".
            var spec = TryDeserializeSeed(jsonString) ?? new TaxonomySpecification { Version = "v1.0" };
            var initial = new TaxonomyDocument
            {
                Version = string.IsNullOrWhiteSpace(spec.Version) ? "v1.0" : spec.Version,
                UpdatedAt = DateTimeOffset.UtcNow,
                Changes = new[] { "Initial taxonomy version" },
                ProposedFromVideoId = null
            };
            // copy map entries
            foreach (var kv in spec.Taxonomy)
            {
                initial.Taxonomy[kv.Key] = kv.Value;
            }

            var saveResult = await SaveTaxonomyAsync(initial);

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
        // Store only the taxonomy map (no version) for readability
        public Dictionary<string, AchievementTypeGroup> taxonomy { get; set; } = new();
        public DateTimeOffset updatedAt { get; set; }
        public string[] changes { get; set; } = Array.Empty<string>();
        public string? proposedFromVideoId { get; set; }

        public static CosmosTaxonomyDocument FromTaxonomyDocument(TaxonomyDocument document)
        {
            return new CosmosTaxonomyDocument
            {
                id = document.Version,
                taxonomy = new Dictionary<string, AchievementTypeGroup>(document.Taxonomy, StringComparer.OrdinalIgnoreCase),
                updatedAt = document.UpdatedAt,
                changes = document.Changes,
                proposedFromVideoId = document.ProposedFromVideoId
            };
        }

        public TaxonomyDocument ToTaxonomyDocument()
        {
            var doc = new TaxonomyDocument
            {
                Version = id,
                UpdatedAt = updatedAt,
                Changes = changes,
                ProposedFromVideoId = proposedFromVideoId
            };
            foreach (var kv in taxonomy)
            {
                doc.Taxonomy[kv.Key] = kv.Value;
            }
            return doc;
        }
    }

    private static TaxonomySpecification? TryDeserializeSeed(string json)
    {
        try
        {
            // Preferred: explicit model with { "version": ..., "taxonomy": { ... } }
            var explicitModel = JsonSerializer.Deserialize<TaxonomySpecification>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (explicitModel is not null && explicitModel.Taxonomy.Count > 0)
                return explicitModel;

            // Fallback: try to parse either wrapped or flat into the explicit model
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var spec = new TaxonomySpecification();
            if (root.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String)
            {
                spec.Version = ver.GetString() ?? spec.Version;
            }
            if (root.TryGetProperty("taxonomy", out var tx) && tx.ValueKind == JsonValueKind.Object)
            {
                foreach (var domain in tx.EnumerateObject())
                {
                    var group = JsonSerializer.Deserialize<AchievementTypeGroup>(domain.Value.GetRawText()) ?? new AchievementTypeGroup();
                    spec.Taxonomy[domain.Name] = group;
                }
                return spec;
            }

            // Flat: domains at root
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var domain in root.EnumerateObject())
                {
                    if (string.Equals(domain.Name, "version", StringComparison.OrdinalIgnoreCase)) continue;
                    var group = JsonSerializer.Deserialize<AchievementTypeGroup>(domain.Value.GetRawText()) ?? new AchievementTypeGroup();
                    spec.Taxonomy[domain.Name] = group;
                }
                return spec;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
