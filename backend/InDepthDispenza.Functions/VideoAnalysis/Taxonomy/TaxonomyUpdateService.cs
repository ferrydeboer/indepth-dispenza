using System.Text.Json;
using System.Text.Json.Nodes;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Taxonomy;

public sealed class TaxonomyUpdateService : ITaxonomyUpdateService
{
    private readonly ILogger<TaxonomyUpdateService> _logger;
    private readonly ITaxonomyRepository _taxonomyRepository;

    public TaxonomyUpdateService(
        ILogger<TaxonomyUpdateService> logger,
        ITaxonomyRepository taxonomyRepository)
    {
        _logger = logger;
        _taxonomyRepository = taxonomyRepository;
    }

    public async Task<ServiceResult<string?>> ApplyProposalsAsync(VideoAnalysis analysis)
    {
        // Only proceed if proposals are present
        if (analysis.Proposals is null || analysis.Proposals.Length == 0)
        {
            return ServiceResult<string?>.Success(null);
        }

        // Load latest taxonomy
        var latestResult = await _taxonomyRepository.GetLatestTaxonomyAsync();
        if (!latestResult.IsSuccess)
        {
            return ServiceResult<string?>.Failure(latestResult.ErrorMessage!, latestResult.Exception);
        }

        var latest = latestResult.Data;
        if (latest is null)
        {
            return ServiceResult<string?>.Failure("No taxonomy available to update.");
        }

        // Parse existing taxonomy JSON into mutable nodes
        var rootNode = JsonNode.Parse(latest.Taxonomy.RootElement.GetRawText()) as JsonObject
                        ?? new JsonObject();

        // Ensure we have a taxonomy object inside the root
        var taxonomyNode = rootNode["taxonomy"] as JsonObject;
        if (taxonomyNode is null)
        {
            taxonomyNode = new JsonObject();
            rootNode["taxonomy"] = taxonomyNode;
        }

        var changeNotes = new List<string>();

        foreach (var proposal in analysis.Proposals)
        {
            var domainName = proposal.AchievementCategory; // e.g., "healing"
            if (string.IsNullOrWhiteSpace(domainName))
                continue;

            // Ensure domain object exists
            var domainNode = taxonomyNode[domainName] as JsonObject;
            if (domainNode is null)
            {
                domainNode = new JsonObject();
                taxonomyNode[domainName] = domainNode;
                changeNotes.Add($"Add domain '{domainName}'");
            }

            // Proposal group contains categories under the domain
            foreach (var kvp in proposal.Group)
            {
                var categoryName = kvp.Key;
                var category = kvp.Value;

                if (domainNode[categoryName] is JsonObject existingCategory)
                {
                    // Merge arrays: subcategories, attributes
                    MergeStringArrayProperty(existingCategory, "subcategories", category.Subcategories);
                    MergeStringArrayProperty(existingCategory, "attributes", category.Attributes);
                    changeNotes.Add($"Merge category '{domainName}.{categoryName}'");
                }
                else
                {
                    var newCategory = new JsonObject();
                    if (category.Subcategories is { Count: > 0 })
                    {
                        newCategory["subcategories"] = new JsonArray(category.Subcategories.Select(s => (JsonNode)s).ToArray());
                    }
                    if (category.Attributes is { Count: > 0 })
                    {
                        newCategory["attributes"] = new JsonArray(category.Attributes.Select(s => (JsonNode)s).ToArray());
                    }
                    domainNode[categoryName] = newCategory;
                    changeNotes.Add($"Add category '{domainName}.{categoryName}'");
                }
            }
        }

        if (changeNotes.Count == 0)
        {
            // Nothing changed effectively
            return ServiceResult<string?>.Success(null);
        }

        // Compute next version id: increment minor version (v1.0 -> v1.1)
        var nextVersionId = IncrementVersion(latest.Id);

        // Build new TaxonomyDocument
        var newJson = JsonDocument.Parse(rootNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        }));

        var newDoc = new TaxonomyDocument(
            Id: nextVersionId,
            Taxonomy: newJson,
            UpdatedAt: DateTimeOffset.UtcNow,
            Changes: changeNotes.ToArray(),
            ProposedFromVideoId: analysis.Id
        );

        var saveResult = await _taxonomyRepository.SaveTaxonomyAsync(newDoc);
        if (!saveResult.IsSuccess)
        {
            return ServiceResult<string?>.Failure(saveResult.ErrorMessage!, saveResult.Exception);
        }

        _logger.LogInformation("Saved new taxonomy version {VersionId} from proposals in video {VideoId}", nextVersionId, analysis.Id);
        return ServiceResult<string?>.Success(nextVersionId);
    }

    private static void MergeStringArrayProperty(JsonObject target, string propertyName, IList<string>? additions)
    {
        if (additions is null || additions.Count == 0)
            return;

        if (target[propertyName] is JsonArray arr)
        {
            var existing = new HashSet<string>(arr.Select(n => n?.GetValue<string>() ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            foreach (var add in additions)
            {
                if (string.IsNullOrWhiteSpace(add)) continue;
                if (existing.Add(add))
                {
                    arr.Add(add);
                }
            }
        }
        else
        {
            target[propertyName] = new JsonArray(additions
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => (JsonNode)s)
                .ToArray());
        }
    }

    private static string IncrementVersion(string current)
    {
        // Expected formats: "v1.0", "v2.3". If parsing fails, fallback to timestamp-based.
        if (current.StartsWith('v') && current.Contains('.'))
        {
            var body = current.Substring(1);
            var parts = body.Split('.', 2);
            if (int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                // increment minor
                minor += 1;
                return $"v{major}.{minor}";
            }
        }
        // Fallback
        return $"v{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }
}
