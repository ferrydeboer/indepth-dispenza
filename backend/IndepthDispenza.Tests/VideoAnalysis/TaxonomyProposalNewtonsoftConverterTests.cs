using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis;

public class TaxonomyProposalNewtonsoftConverterTests
{
    private static TaxonomyProposal MakeProposal()
    {
        var node = new CategoryNode
        {
            Subcategories = new List<string> { "obesity" },
            Attributes = new List<string> { "chronic" }
        };
        var group = new AchievementTypeGroup
        {
            ["physical_health"] = node
        };
        return new TaxonomyProposal("healing", group, "because");
    }

    [Test]
    public void Newtonsoft_Serialize_Emits_DynamicKey_And_Justification()
    {
        var proposal = MakeProposal();
        var json = JsonConvert.SerializeObject(proposal);

        var obj = JObject.Parse(json);
        Assert.That(obj.Property("healing"), Is.Not.Null, "Should include dynamic domain key");
        Assert.That(obj.Property("justification")?.Value?.ToString(), Is.EqualTo("because"));

        var healing = (JObject)obj["healing"]!;
        Assert.That(healing.Property("physical_health"), Is.Not.Null, "Should include category under domain");
    }

    [Test]
    public void Newtonsoft_Roundtrip_Works()
    {
        var json = "{" +
                   "\"healing\": { \"physical_health\": { \"subcategories\": [\"obesity\"], \"attributes\": [\"chronic\"] } }," +
                   "\"justification\": \"because\"}";

        var proposal = JsonConvert.DeserializeObject<TaxonomyProposal>(json)!;
        Assert.That(proposal.AchievementCategory, Is.EqualTo("healing"));
        Assert.That(proposal.Justification, Is.EqualTo("because"));
        Assert.That(proposal.Group.ContainsKey("physical_health"), Is.True);

        var backJson = JsonConvert.SerializeObject(proposal);
        var back = JObject.Parse(backJson);
        Assert.That(back.Property("healing"), Is.Not.Null);
        Assert.That(back.Property("justification")?.Value?.ToString(), Is.EqualTo("because"));
    }
}
