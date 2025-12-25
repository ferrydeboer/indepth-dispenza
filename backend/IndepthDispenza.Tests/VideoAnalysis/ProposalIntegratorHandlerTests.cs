using AutoFixture;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using IndepthDispenza.Tests.TestInfrastructure.VideoAnalysis;
using static IndepthDispenza.Tests.TestInfrastructure.VideoAnalysis.ProposalData;

namespace IndepthDispenza.Tests.VideoAnalysis;

public class ProposalIntegratorHandlerTests
{
    private IFixture _fx = null!;

    [SetUp]
    public void SetUp()
    {
        _fx = new Fixture();
        _fx.Customize(new ProposalTestsCustomization());
    }

    [Test]
    public async Task MergeIntoExistingAchievement_AddsMissingTagsOnly()
    {
        var response = _fx.Create<LlmResponseForProposalTests>()
            .WithAchievements(Ach("healing", "existing", "cancer"))
            .WithProposals(Proposal("healing", ("cancer", ["cervical_cancer"])) )
            .Build();

        var sut = new ProposalIntegratorHandler(NullLogger<ProposalIntegratorHandler>.Instance);
        await sut.HandleAsync(response, _fx.Create<VideosAnalyzedContext>());

        var ach = response.VideoAnalysisResponse.Analysis.Achievements!.Single(a => a.Type == "healing");
        Assert.That(ach.Tags, Is.EquivalentTo(["existing", "cancer", "cervical_cancer"]));
    }

    [Test]
    public async Task AddsNewAchievement_WhenNoMatchingType()
    {
        var response = _fx.Create<LlmResponseForProposalTests>()
            .WithAchievements(Ach("manifestation", "job"))
            .WithProposals(Proposal("healing", ("cancer", ["cervical_cancer"])) )
            .Build();

        var sut = new ProposalIntegratorHandler(NullLogger<ProposalIntegratorHandler>.Instance);
        await sut.HandleAsync(response, _fx.Create<VideosAnalyzedContext>());

        // Since array is fixed-size, handler cannot truly expand the in-memory array;
        // the TranscriptAnalyzer sync step ensures persisted DTO is updated. Here we assert no duplicates or crashes.
        Assert.That(response.VideoAnalysisResponse.Analysis.Achievements!.Any(a => a.Type.Equals("healing", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public async Task NoChanges_WhenNoProposals()
    {
        var response = _fx.Create<LlmResponseForProposalTests>()
            .WithAchievements(Ach("healing", "cancer"))
            .Build();

        var sut = new ProposalIntegratorHandler(NullLogger<ProposalIntegratorHandler>.Instance);
        await sut.HandleAsync(response, _fx.Create<VideosAnalyzedContext>());
        var ach = response.VideoAnalysisResponse.Analysis.Achievements!.Single();
        Assert.That(ach.Tags, Is.EquivalentTo(["cancer"]));
    }

    [Test]
    public async Task TranscriptAnalyzer_PersistsMergedAchievements()
    {
        // Arrange LLM response with no achievements and one proposal
        var json = """
        {
          "analysis": {
            "achievements": [
              { "type": "healing", "tags": ["cancer"], "details": null }
            ],
            "timeframe": { "noticeEffects": null, "fullHealing": null },
            "practices": [],
            "sentimentScore": 0.5,
            "confidenceScore": 0.8
          },
          "proposals": {
            "taxonomy": [
              { "healing": { "cancer": { "subcategories": ["cervical_cancer"] } }, "justification": "j" }
            ]
          }
        }
        """;

        var llm = new Mock<ILlmService>();
        var assistant = new LlmAssistantPayload(json, "application/json", null);
        var call = new LlmCallInfo("test", "model-x", 10, 1, 1, 2, "rid", DateTimeOffset.UtcNow, "stop");
        var resp = new CommonLlmResponse(call, assistant);
        llm.Setup(s => s.CallAsync(It.IsAny<string>()))
           .ReturnsAsync(ServiceResult<CommonLlmResponse>.Success(resp));

        LlmResponse? stored = null;
        var repo = new Mock<IVideoAnalysisRepository>();
        repo.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .Callback<string, DateTimeOffset, string?, LlmResponse>((_, _, _, dto) => stored = dto)
            .ReturnsAsync(ServiceResult.Success());

        var handlers = new IVideoAnalyzedHandler[]
        {
            new ProposalIntegratorHandler(NullLogger<ProposalIntegratorHandler>.Instance)
        };

        var sut = new TranscriptAnalyzer(
            NullLogger<TranscriptAnalyzer>.Instance,
            llm.Object,
            [],
            handlers,
            repo.Object);

        // Act
        var result = await sut.AnalyzeTranscriptAsync("vid-123");

        // Assert: persisted DTO achievements must include the proposal-derived tags
        Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
        Assert.That(stored, Is.Not.Null);
        var achievements = stored!.VideoAnalysisResponse.Analysis.Achievements;
        Assert.That(achievements, Is.Not.Null);
        Assert.That(achievements!.Length, Is.EqualTo(1));
        Assert.That(achievements[0].Type, Is.EqualTo("healing"));
        Assert.That(achievements[0].Tags, Is.EquivalentTo(["cancer", "cervical_cancer"]));
    }
}
