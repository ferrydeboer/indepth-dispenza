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
            // Ensure seed is applied if it's newer than what's stored
            await SeedTaxonomyIfNeededAsync();

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

            return ServiceResult<TaxonomyDocument?>.Failure("Expected a taxonomy by now.");
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
    /// Ensures the taxonomy in Cosmos is at least the version provided in the seed file.
    /// If Cosmos is empty, seeds the seed version. If Cosmos has an older version than the seed,
    /// upgrades by inserting the seed version as a new document. Thread-safe via semaphore.
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

            Logger.LogInformation("Checking if taxonomy needs seeding or upgrade");

            if (!File.Exists(_taxonomySeedFilePath))
            {
                Logger.LogWarning("Taxonomy seed file not found at {FilePath}", _taxonomySeedFilePath);
                _taxonomySeeded = true; // Don't try again
                return;
            }

            Logger.LogInformation("Reading taxonomy seed from {FilePath}", _taxonomySeedFilePath);

            var jsonString = await File.ReadAllTextAsync(_taxonomySeedFilePath);
            // Seed can be either flat map or wrapped under {"taxonomy": ...} and may contain a top-level "version".
            var spec = TryDeserializeSeed(jsonString) ?? new TaxonomySpecification { Version = new TaxonomyVersion(1, 0) };
            var seedVersion = spec.Version;

            // Determine latest stored version in Cosmos
            var container = await GetOrCreateContainerAsync();
            var queryDefinition = new QueryDefinition("SELECT TOP 1 c.id, c.updatedAt FROM c ORDER BY c.updatedAt DESC");
            var iterator = container.GetItemQueryIterator<CosmosTaxonomyDocument>(queryDefinition);
            CosmosTaxonomyDocument? latestDoc = null;
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                latestDoc = response.FirstOrDefault();
            }

            bool shouldSeed;
            if (latestDoc is null)
            {
                Logger.LogInformation("No taxonomy found in Cosmos. Will seed version {SeedVersion}.", seedVersion);
                shouldSeed = true;
            }
            else
            {
                var latestVersion = (TaxonomyVersion)latestDoc.id;
                if (seedVersion > latestVersion)
                {
                    Logger.LogInformation("Seed version {SeedVersion} is newer than latest in Cosmos {LatestVersion}. Will upgrade.", seedVersion, latestVersion);
                    shouldSeed = true;
                }
                else
                {
                    Logger.LogInformation("Seed version {SeedVersion} is not newer than Cosmos {LatestVersion}. No action.", seedVersion, latestVersion);
                    shouldSeed = false;
                }
            }

            if (shouldSeed)
            {
                var newDoc = new TaxonomyDocument
                {
                    Version = seedVersion,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Changes = new[] { latestDoc is null ? "Initial taxonomy version" : $"Upgraded taxonomy to seed version {seedVersion}" },
                    ProposedFromVideoId = null
                };
                // copy map entries
                foreach (var kv in spec.Taxonomy)
                {
                    newDoc.Taxonomy[kv.Key] = kv.Value;
                }

                var saveResult = await SaveTaxonomyAsync(newDoc);

                if (saveResult.IsSuccess)
                {
                    Logger.LogInformation("Successfully saved taxonomy version {Version}", newDoc.Version);
                }
                else
                {
                    Logger.LogError("Failed to save taxonomy: {Error}", saveResult.ErrorMessage);
                }
            }

            _taxonomySeeded = true;
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
                id = document.Version.ToString(),
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
                Version = (TaxonomyVersion)id,
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
                var v = ver.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    spec.Version = TaxonomyVersion.Parse(v!);
                }
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
