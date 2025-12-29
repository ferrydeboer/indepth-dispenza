using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            // Use the shared, non-seeding helper to avoid any recursion/duplication
            var latestDoc = await QueryLatestTaxonomyDocAsync(includeBody: true);
            if (latestDoc is not null)
            {
                var document = latestDoc.ToTaxonomyDocument();
                Logger.LogInformation("Successfully retrieved latest taxonomy version: {VersionId}", document.Version);
                return ServiceResult<TaxonomyDocument?>.Success(document);
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

            // Determine latest stored version in Cosmos without calling public methods to avoid recursion.
            // We use a minimal internal helper that never triggers seeding.
            var latestDoc = await QueryLatestTaxonomyDocAsync(includeBody: false);

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

    /// <summary>
    /// Internal helper to fetch the latest taxonomy document directly from Cosmos without invoking seeding.
    /// Why: Avoids infinite recursion and duplication. Both seeding and read paths share this low-level query,
    /// but only the public read method (<see cref="GetLatestTaxonomyAsync"/>) orchestrates seeding beforehand.
    /// The helper can optionally include the full document body (taxonomy map) or just lightweight metadata.
    /// </summary>
    /// <param name="includeBody">When true, selects the full document; when false, selects only id/updatedAt.</param>
    /// <returns>The latest CosmosTaxonomyDocument or null if none exist.</returns>
    private async Task<CosmosTaxonomyDocument?> QueryLatestTaxonomyDocAsync(bool includeBody)
    {
        var container = await GetOrCreateContainerAsync();

        // Note: selecting explicit fields reduces RU/s when only metadata is needed during seeding.
        var queryText = includeBody
            ? "SELECT TOP 1 * FROM c ORDER BY c.updatedAt DESC"
            : "SELECT TOP 1 c.id, c.updatedAt FROM c ORDER BY c.updatedAt DESC";

        var queryDefinition = new QueryDefinition(queryText);
        var iterator = container.GetItemQueryIterator<CosmosTaxonomyDocument>(queryDefinition);

        if (!iterator.HasMoreResults)
            return null;

        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
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

    private TaxonomySpecification? TryDeserializeSeed(string json)
    {
        try
        {
            // Use Newtonsoft.Json to deserialize so converters/attributes match Cosmos configuration
            // First attempt: strongly typed model with expected shape { "id": ..., "taxonomy": { ... } }
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            var explicitModel = JsonConvert.DeserializeObject<TaxonomySpecification>(json, settings);
            if (explicitModel is not null && explicitModel.Taxonomy.Count > 0)
                return explicitModel;

            // Fallback: handle wrapped or flat structures using LINQ-to-JSON
            // This should be deleted, is obsolete by now.
            var root = JToken.Parse(json);
            var spec = new TaxonomySpecification();

            // version can be under "id" (string like "v2.0")
            var idToken = root["id"];
            if (idToken != null && idToken.Type == JTokenType.String)
            {
                var v = idToken.Value<string>();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    spec.Version = TaxonomyVersion.Parse(v!);
                }
            }

            // taxonomy may be wrapped under a property
            var taxonomyToken = root["taxonomy"];
            if (taxonomyToken != null && taxonomyToken.Type == JTokenType.Object)
            {
                foreach (var prop in ((JObject)taxonomyToken).Properties())
                {
                    var group = prop.Value.ToObject<AchievementTypeGroup>() ?? new AchievementTypeGroup();
                    spec.Taxonomy[prop.Name] = group;
                }
                return spec;
            }

            // Or the taxonomy can be flat at the root: { "domain": { ... }, ... }
            if (root.Type == JTokenType.Object)
            {
                foreach (var prop in ((JObject)root).Properties())
                {
                    if (string.Equals(prop.Name, "version", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(prop.Name, "rules", StringComparison.OrdinalIgnoreCase)) continue;

                    var group = prop.Value.ToObject<AchievementTypeGroup>() ?? new AchievementTypeGroup();
                    spec.Taxonomy[prop.Name] = group;
                }
                return spec;
            }

            return null;
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "Error reading taxonomy seed file");
            return null;
        }
    }
}
