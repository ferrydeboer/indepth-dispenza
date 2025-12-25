using AutoFixture;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;

namespace IndepthDispenza.Tests.TestInfrastructure.VideoAnalysis;

public sealed class LlmResponseForProposalTests
{
    private readonly IFixture _fixture;
    private readonly List<Achievement> _achievements = new();
    private readonly List<TaxonomyProposal> _proposals = new();

    public LlmResponseForProposalTests(IFixture fixture)
    {
        _fixture = fixture;
    }

    public LlmResponseForProposalTests WithAchievements(params Achievement[] items)
    {
        if (items is { Length: > 0 })
            _achievements.AddRange(items);
        return this;
    }

    public LlmResponseForProposalTests WithProposal(TaxonomyProposal proposal)
    {
        if (proposal is not null)
            _proposals.Add(proposal);
        return this;
    }

    public LlmResponseForProposalTests WithProposals(params TaxonomyProposal[] items)
    {
        if (items is { Length: > 0 })
            _proposals.AddRange(items);
        return this;
    }

    public LlmResponse Build()
    {
        var analysis = new AnalysisDto(
            Achievements: _achievements.ToArray(),
            Timeframe: null,
            Practices: Array.Empty<string>(),
            SentimentScore: 0.0,
            ConfidenceScore: 0.0);

        var proposals = new ProposalsDto(
            Taxonomy: _proposals.ToArray());

        var dto = new LlmVideoAnalysisResponseDto(analysis, proposals);
        var prompt = _fixture.Create<PromptExecutionInfoDto>();
        return new LlmResponse(prompt, dto);
    }
}
