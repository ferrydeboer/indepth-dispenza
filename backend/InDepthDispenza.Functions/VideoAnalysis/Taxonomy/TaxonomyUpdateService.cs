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

    public async Task<ServiceResult<string?>> ApplyProposalsAsync(string videoId, TaxonomyProposal[] proposals)
    {
        // Only proceed if proposals are present
        if (proposals is null || proposals.Length == 0)
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

        // Work directly with strong-typed taxonomy (latest inherits TaxonomySpecification)
        var spec = new TaxonomySpecification { Version = latest.Version };
        // Copy existing taxonomy map
        foreach (var kv in latest.Taxonomy)
        {
            spec.Taxonomy[kv.Key] = kv.Value;
        }
        // Copy existing rules to preserve them across updates
        foreach (var rule in latest.Rules)
        {
            spec.Rules[rule.Key] = rule.Value;
        }

        var changeNotes = new List<string>();

        foreach (var proposal in proposals)
        {
            var domainName = proposal.AchievementCategory; // e.g., "healing"
            if (string.IsNullOrWhiteSpace(domainName))
                continue;

            changeNotes.Add(proposal.Justification);
            // Ensure domain group exists
            if (!spec.Taxonomy.TryGetValue(domainName, out var domainGroup))
            {
                domainGroup = new AchievementTypeGroup();
                spec.Taxonomy[domainName] = domainGroup;
                changeNotes.Add($"Add domain '{domainName}'");
            }

            // Proposal group contains categories under the domain
            foreach (var kvp in proposal.Group)
            {
                var categoryName = kvp.Key;
                var proposalNode = kvp.Value;

                if (domainGroup.TryGetValue(categoryName, out var existingCategory))
                {
                    // Merge arrays: subcategories, attributes
                    existingCategory = existingCategory ?? new CategoryNode();
                    var merged = MergeCategory(existingCategory, proposalNode);
                    domainGroup[categoryName] = merged;
                    changeNotes.Add($"Merge category '{domainName}.{categoryName}'");
                }
                else
                {
                    // Insert new category
                    domainGroup[categoryName] = new CategoryNode
                    {
                        Subcategories = proposalNode.Subcategories?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        Attributes = proposalNode.Attributes?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    };
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
        var nextVersionId = latest.Version.IncrementMinor();

        // Build new TaxonomyDocument with strong types only
        var newDoc = new TaxonomyDocument
        {
            Version = nextVersionId,
            UpdatedAt = DateTimeOffset.UtcNow,
            Changes = changeNotes.ToArray(),
            ProposedFromVideoId = videoId
        };
        foreach (var kv in spec.Taxonomy)
        {
            newDoc.Taxonomy[kv.Key] = kv.Value;
        }
        foreach (var kv in spec.Rules)
        {
            newDoc.Rules[kv.Key] = kv.Value;
        }

        var saveResult = await _taxonomyRepository.SaveTaxonomyAsync(newDoc);
        if (!saveResult.IsSuccess)
        {
            return ServiceResult<string?>.Failure(saveResult.ErrorMessage!, saveResult.Exception);
        }

        _logger.LogInformation("Saved new taxonomy version {VersionId} from proposals in video {VideoId}", nextVersionId, videoId);
        return ServiceResult<string?>.Success(nextVersionId);
    }

    private static CategoryNode MergeCategory(CategoryNode existing, CategoryNode proposal)
    {
        List<string>? MergeLists(List<string>? current, List<string>? incoming)
        {
            if (incoming is null || incoming.Count == 0)
                return current;

            var set = new HashSet<string>(current ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var item in incoming)
            {
                if (NotNullOrWhiteSpace(item)) set.Add(item);
            }
            return set.ToList();
        }

        return new CategoryNode
        {
            Subcategories = MergeLists(existing.Subcategories, proposal.Subcategories),
            Attributes = MergeLists(existing.Attributes, proposal.Attributes)
        };
    }

    private static bool NotNullOrWhiteSpace(string? s) => !string.IsNullOrWhiteSpace(s);
}
