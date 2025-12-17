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
    private static TaxonomyDocument BuildInitialTaxonomy(bool includeHealing = true, bool includePhysicalHealth = true)
    {
        var doc = new TaxonomyDocument
        {
            Version = "v1.0",
            UpdatedAt = DateTimeOffset.UtcNow,
            Changes = Array.Empty<string>()
        };

        if (includeHealing)
        {
            var healing = new AchievementTypeGroup();
            doc.Taxonomy["healing"] = healing;

            if (includePhysicalHealth)
            {
                healing["physical_health"] = new CategoryNode
                {
                    Subcategories = new List<string> { "obesity" },
                    Attributes = new List<string> { "chronic" }
                };
            }
        }

        return doc;
    }

    private sealed class SaveHolder { public TaxonomyDocument? Saved { get; set; } }

    private static (TaxonomyUpdateService service, Mock<ITaxonomyRepository> repoMock, SaveHolder holder) CreateService(
        TaxonomyDocument initialTaxonomy)
    {
        var repoMock = new Mock<ITaxonomyRepository>();
        var holder = new SaveHolder();
        var latest = initialTaxonomy;

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

    private static AchievementTypeGroup GetDomain(TaxonomyDocument doc, string domain)
    {
        Assert.That(doc.Taxonomy.ContainsKey(domain), Is.True, $"Domain '{domain}' should exist");
        return doc.Taxonomy[domain];
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

        var domainNode = GetDomain(holder.Saved!, "new_domain");
        Assert.That(domainNode.ContainsKey("new_category"), Is.True);
        var categoryNode = domainNode["new_category"];
        Assert.That(categoryNode.Subcategories ?? new List<string>(), Is.EquivalentTo(new[] { "sub1", "sub2" }));
        Assert.That(categoryNode.Attributes ?? new List<string>(), Is.EquivalentTo(new[] { "attr1" }));
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

        var healing = GetDomain(holder.Saved!, "healing");
        Assert.That(healing.ContainsKey("mental_health"), Is.True);
        var cat = healing["mental_health"];
        Assert.That(cat.Subcategories ?? new List<string>(), Is.EquivalentTo(new[] { "anxiety" }));
        Assert.That(cat.Attributes ?? new List<string>(), Is.EquivalentTo(new[] { "chronic" }));
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

        var healing = GetDomain(holder.Saved!, "healing");
        var ph = healing["physical_health"];
        Assert.That(ph.Subcategories ?? new List<string>(), Is.EquivalentTo(new[] { "obesity", "diabetes" }));
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

        var healing = GetDomain(holder.Saved!, "healing");
        var ph = healing["physical_health"];
        Assert.That(ph.Attributes ?? new List<string>(), Is.EquivalentTo(new[] { "chronic", "acute" }));
    }
}
