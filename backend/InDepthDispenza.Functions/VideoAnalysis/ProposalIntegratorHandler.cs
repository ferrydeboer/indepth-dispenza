using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Integrates taxonomy <see cref="TaxonomyProposal"/> items into the in-memory <see cref="VideoAnalysis.Achievements"/>.
/// This exists because current model prompting does not yet reliably include proposed taxonomy tags in the
/// produced achievements. Until that instruction sticks, this handler merges proposal tags into matching
/// achievements (type/category match) or adds a new achievement when none exists, without creating duplicates.
/// </summary>
public sealed class ProposalIntegratorHandler : IVideoAnalyzedHandler
{
    private readonly ILogger<ProposalIntegratorHandler> _logger;

    public ProposalIntegratorHandler(ILogger<ProposalIntegratorHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(LlmResponse response, VideosAnalyzedContext context)
    {
        var proposals = response.VideoAnalysisResponse.Proposals.Taxonomy;
        if (proposals is null || proposals.Length == 0)
        {
            return Task.CompletedTask;
        }

        // Build case-insensitive map of existing achievements by type (domain)
        var comparer = StringComparer.OrdinalIgnoreCase;
        var byType = new Dictionary<string, int>(comparer);
        var achievements = response.VideoAnalysisResponse.Analysis.Achievements ?? Array.Empty<Achievement>();
        for (int i = 0; i < achievements.Length; i++)
        {
            var type = achievements[i].Type;
            if (!byType.ContainsKey(type))
                byType[type] = i;
        }

        foreach (var proposal in proposals)
        {
            var domain = proposal.AchievementCategory;
            var proposedTags = ExtractTags(proposal.Group);

            if (proposedTags.Count == 0)
                continue;

            if (byType.TryGetValue(domain, out var idx))
            {
                // Merge into existing achievement (keeping original order and avoiding duplicates)
                // Work on underlying array reference to avoid reconstructing DTO graphs
                var achArray = response.VideoAnalysisResponse.Analysis.Achievements;
                if (achArray is null || idx < 0 || idx >= achArray.Length)
                    continue;
                var ach = achArray[idx];
                var tags = ach.Tags ?? Array.Empty<string>();
                var existing = new HashSet<string>(tags, comparer);
                var merged = new List<string>(tags.Length + proposedTags.Count);
                merged.AddRange(tags);
                foreach (var t in proposedTags)
                {
                    if (existing.Add(t))
                        merged.Add(t);
                }

                // Replace element in-place (array is mutable, records are immutable)
                achArray[idx] = ach with { Tags = merged.ToArray() };
            }
            else
            {
                // Do not add new achievements here; proposals without an existing matching
                // achievement will be handled during the persistence/sync step in TranscriptAnalyzer.
                context.Logger.LogDebug("No existing achievement for domain {Domain}; skipping in-memory add.", domain);
            }
        }

        return Task.CompletedTask;
    }


    internal static HashSet<string> ExtractTags(AchievementTypeGroup group)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in group)
        {
            var cat = kv.Key;
            if (!string.IsNullOrWhiteSpace(cat))
                tags.Add(cat);

            var node = kv.Value;
            if (node?.Subcategories != null)
            {
                foreach (var sub in node.Subcategories)
                {
                    if (!string.IsNullOrWhiteSpace(sub))
                        tags.Add(sub);
                }
            }
        }
        return tags;
    }
}
