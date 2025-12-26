using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;

namespace IndepthDispenza.Tests.TestInfrastructure.VideoAnalysis;

public static class ProposalData
{
    public static Achievement Ach(string type, params string[] tags) => new(type, tags, null);

    public static TaxonomyProposal Proposal(string domain, params (string category, string[] subs)[] items)
    {
        var group = new AchievementTypeGroup();
        foreach (var (cat, subs) in items)
        {
            group[cat] = new CategoryNode { Subcategories = subs.ToList() };
        }
        return new TaxonomyProposal(domain, group, "because");
    }
}
