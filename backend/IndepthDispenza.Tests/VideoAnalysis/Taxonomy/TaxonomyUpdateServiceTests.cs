using System.Text.Json;
using System.Text.Json.Nodes;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Prompting;
using InDepthDispenza.Functions.VideoAnalysis.Taxonomy;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis.Taxonomy;

public class TaxonomyUpdateServiceTests
{
    private static JsonDocument BuildInitialTaxonomy(bool includeHealing = true, bool includePhysicalHealth = true)
    {
        var root = new JsonObject();
        var taxonomy = new JsonObject();
        root["taxonomy"] = taxonomy;

        if (includeHealing)
        {
            var healing = new JsonObject();
            taxonomy["healing"] = healing;

            if (includePhysicalHealth)
            {
                var physical = new JsonObject
                {
                    ["subcategories"] = new JsonArray("obesity"),
                    ["attributes"] = new JsonArray("chronic")
                };
                healing["physical_health"] = physical;
            }
        }

        return JsonDocument.Parse(root.ToJsonString());
    }

    private sealed class SaveHolder { public TaxonomyDocument? Saved { get; set; } }

    private static (TaxonomyUpdateService service, Mock<ITaxonomyRepository> repoMock, SaveHolder holder) CreateService(
        JsonDocument initialTaxonomy)
    {
        var repoMock = new Mock<ITaxonomyRepository>();
        var holder = new SaveHolder();

        var latest = new TaxonomyDocument(
            Id: "v1.0",
            Taxonomy: initialTaxonomy,
            UpdatedAt: DateTimeOffset.UtcNow,
            Changes: []);

        repoMock
            .Setup(r => r.GetLatestTaxonomyAsync())
            .ReturnsAsync(ServiceResult<TaxonomyDocument?>.Success(latest));

        repoMock
            .Setup(r => r.SaveTaxonomyAsync(It.IsAny<TaxonomyDocument>()))
            .Callback<TaxonomyDocument>(d => holder.Saved = d)
            .ReturnsAsync(ServiceResult.Success());

        var service = new TaxonomyUpdateService(NullLogger<TaxonomyUpdateService>.Instance, repoMock.Object);
        return (service, repoMock, holder);
    }

    private static InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis MakeAnalysis(string videoId, params TaxonomyProposal[] proposals)
    {
        return new InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis(
            Id: videoId,
            AnalyzedAt: DateTimeOffset.UtcNow,
            ModelVersion: "test-model",
            Achievements: [],
            Timeframe: null,
            Practices: [],
            SentimentScore: 0.0,
            ConfidenceScore: 0.0,
            Proposals: proposals);
    }

    private static TaxonomyProposal Proposal(string domain,
        string category,
        IEnumerable<string>? subcategories = null,
        IEnumerable<string>? attributes = null)
    {
        var node = new CategoryNode
        {
            Subcategories = subcategories?.ToList(),
            Attributes = attributes?.ToList()
        };
        var group = new AchievementTypeGroup
        {
            [category] = node
        };
        return new TaxonomyProposal(domain, group, "because");
    }

    private static JsonObject GetTaxonomyRoot(JsonDocument doc)
    {
        var root = JsonNode.Parse(doc.RootElement.GetRawText()) as JsonObject;
        Assert.That(root, Is.Not.Null);
        var taxonomy = root!["taxonomy"] as JsonObject;
        Assert.That(taxonomy, Is.Not.Null);
        return taxonomy!;
    }

    [Test]
    public async Task ApplyProposals_AddsFullHierarchy_NewDomainAndCategory()
    {
        // Arrange: start with empty taxonomy (no healing domain)
        var initial = BuildInitialTaxonomy(includeHealing: false);
        var (service, repo, holder) = CreateService(initial);

        var analysis = MakeAnalysis(
            "vid-1",
            Proposal(
                domain: "new_domain",
                category: "new_category",
                subcategories: ["sub1", "sub2"],
                attributes: ["attr1"]));

        // Act
        var result = await service.ApplyProposalsAsync(analysis);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Data, Is.EqualTo("v1.1"));
        repo.Verify(r => r.SaveTaxonomyAsync(It.IsAny<TaxonomyDocument>()), Times.Once);
        Assert.That(holder.Saved, Is.Not.Null);
        Assert.That(holder.Saved!.ProposedFromVideoId, Is.EqualTo("vid-1"));
        Assert.That(holder.Saved!.Changes, Does.Contain("Add domain 'new_domain'"));
        Assert.That(holder.Saved!.Changes, Does.Contain("Add category 'new_domain.new_category'"));

        var taxonomy = GetTaxonomyRoot(holder.Saved.Taxonomy);
        var domainNode = taxonomy["new_domain"] as JsonObject;
        Assert.That(domainNode, Is.Not.Null);
        var categoryNode = domainNode!["new_category"] as JsonObject;
        Assert.That(categoryNode, Is.Not.Null);
        var subs = categoryNode!["subcategories"] as JsonArray;
        var attrs = categoryNode!["attributes"] as JsonArray;
        Assert.That(subs, Is.Not.Null);
        Assert.That(attrs, Is.Not.Null);
        Assert.That(subs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["sub1", "sub2"]));
        Assert.That(attrs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["attr1"]));
    }

    [Test]
    public async Task ApplyProposals_AddsCategory_UnderExistingDomain()
    {
        // Arrange: taxonomy has healing domain but not the new category
        var initial = BuildInitialTaxonomy(includeHealing: true, includePhysicalHealth: false);
        var (service, _, holder) = CreateService(initial);

        var analysis = MakeAnalysis(
            "vid-2",
            Proposal(
                domain: "healing",
                category: "mental_health",
                subcategories: ["anxiety"],
                attributes: ["chronic"]));

        // Act
        var result = await service.ApplyProposalsAsync(analysis);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Data, Is.EqualTo("v1.1"));
        Assert.That(holder.Saved, Is.Not.Null);
        Assert.That(holder.Saved!.Changes, Does.Contain("Add category 'healing.mental_health'"));

        var taxonomy = GetTaxonomyRoot(holder.Saved.Taxonomy);
        var healing = taxonomy["healing"] as JsonObject;
        Assert.That(healing, Is.Not.Null);
        var cat = healing!["mental_health"] as JsonObject;
        Assert.That(cat, Is.Not.Null);
        var subs = cat!["subcategories"] as JsonArray;
        var attrs = cat!["attributes"] as JsonArray;
        Assert.That(subs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["anxiety"]));
        Assert.That(attrs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["chronic"]));
    }

    [Test]
    public async Task ApplyProposals_MergesSubcategory_IntoExistingCategory()
    {
        // Arrange: taxonomy has healing.physical_health with one subcategory
        var initial = BuildInitialTaxonomy();
        var (service, _, holder) = CreateService(initial);

        var analysis = MakeAnalysis(
            "vid-3",
            Proposal(
                domain: "healing",
                category: "physical_health",
                subcategories: ["diabetes"]));

        // Act
        var result = await service.ApplyProposalsAsync(analysis);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(holder.Saved, Is.Not.Null);
        Assert.That(holder.Saved!.Changes, Does.Contain("Merge category 'healing.physical_health'"));

        var taxonomy = GetTaxonomyRoot(holder.Saved.Taxonomy);
        var subs = ((taxonomy["healing"] as JsonObject)!["physical_health"] as JsonObject)!["subcategories"] as JsonArray;
        Assert.That(subs, Is.Not.Null);
        // Should contain existing + new without duplicates
        Assert.That(subs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["obesity", "diabetes"]));
    }

    [Test]
    public async Task ApplyProposals_MergesAttribute_IntoExistingCategory()
    {
        // Arrange: taxonomy has healing.physical_health with one attribute
        var initial = BuildInitialTaxonomy();
        var (service, _, holder) = CreateService(initial);

        var analysis = MakeAnalysis(
            "vid-4",
            Proposal(
                domain: "healing",
                category: "physical_health",
                attributes: ["acute"]));

        // Act
        var result = await service.ApplyProposalsAsync(analysis);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(holder.Saved, Is.Not.Null);
        Assert.That(holder.Saved!.Changes, Does.Contain("Merge category 'healing.physical_health'"));

        var taxonomy = GetTaxonomyRoot(holder.Saved.Taxonomy);
        var attrs = ((taxonomy["healing"] as JsonObject)!["physical_health"] as JsonObject)!["attributes"] as JsonArray;
        Assert.That(attrs, Is.Not.Null);
        Assert.That(attrs!.Select(n => n!.GetValue<string>()), Is.EquivalentTo(["chronic", "acute"]));
    }
}
