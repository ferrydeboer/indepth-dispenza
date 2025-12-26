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
        var byType = new Dictionary<string, List<int>>(comparer);
        var achievements = response.VideoAnalysisResponse.Analysis.Achievements ?? Array.Empty<Achievement>();
        for (int i = 0; i < achievements.Length; i++)
        {
            var type = achievements[i].Type;
            if (!byType.TryGetValue(type, out var list))
            {
                list = [];
                byType[type] = list;
            }
            list.Add(i);
        }

        foreach (var proposal in proposals)
        {
            var domain = proposal.AchievementCategory;
            var proposedTags = ExtractTags(proposal.Group);

            if (proposedTags.Count == 0)
                continue;

            if (byType.TryGetValue(domain, out var indices) && indices.Count > 0)
            {
                // Choose the specific achievement by matching the category presence as well
                // Categories are the top-level keys in the group
                var categories = new HashSet<string>(proposal.Group.Select(kv => kv.Key), comparer);

                int targetIdx = -1;
                var achArray = response.VideoAnalysisResponse.Analysis.Achievements;
                foreach (var i in indices)
                {
                    if (achArray is null || i < 0 || i >= achArray.Length)
                        continue;
                    var t = achArray[i].Tags ?? [];
                    // match if any category is already present among tags
                    if (t.Any(tag => categories.Contains(tag)))
                    {
                        targetIdx = i;
                        break;
                    }
                }

                if (targetIdx >= 0)
                {
                    // Merge into existing achievement (keeping original order and avoiding duplicates)
                    // Work on underlying array reference to avoid reconstructing DTO graphs
                    if (achArray is null || targetIdx < 0 || targetIdx >= achArray.Length)
                        continue;
                    var ach = achArray[targetIdx];
                    var tags = ach.Tags;
                    var existing = new HashSet<string>(tags, comparer);
                    var merged = new List<string>(tags.Length + proposedTags.Count);
                    merged.AddRange(tags);
                    foreach (var t in proposedTags)
                    {
                        if (existing.Add(t))
                            merged.Add(t);
                    }

                    // Replace element in-place (array is mutable, records are immutable)
                    achArray[targetIdx] = ach with { Tags = merged.ToArray() };
                }
                else
                {
                    // No achievement for this type has the proposed category; skip in-memory merge
                    context.Logger.LogDebug("No existing achievement for domain {Domain} with matching category; skipping merge.", domain);
                }
            }
            else
            {
                // No achievement exists for this domain/type yet and Achievements array is fixed-size (init-only).
                // Replace the first available achievement slot with a new achievement of the proposal's domain.
                // This ensures downstream persistence will carry the correct domain and tags without duplicating entries in-memory.
                var newTags = new List<string>(1 + proposedTags.Count) { domain };
                newTags.AddRange(proposedTags);

                var achArray = response.VideoAnalysisResponse.Analysis.Achievements;
                if (achArray is { Length: > 0 })
                {
                    achArray[0] = new Achievement(domain, newTags.ToArray(), null);
                    // Maintain index map so subsequent proposals of the same domain can merge
                    byType[domain] = [0];
                }
                else
                {
                    // Nothing to replace; nothing we can do in-memory due to init-only property
                    context.Logger.LogDebug("No existing achievements array to replace for domain {Domain}; skipping in-memory add.", domain);
                }

                context.Logger.LogDebug("Replaced first achievement with new domain {Domain} (tags: {Count}).", domain, newTags.Count);
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
