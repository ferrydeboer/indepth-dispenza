using AutoFixture;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndepthDispenza.Tests.TestInfrastructure.VideoAnalysis;

public sealed class ProposalTestsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // Defaults for primitives/leafs
        fixture.Register<ILogger>(() => NullLogger.Instance);
        fixture.Register(() => new VideosAnalyzedContext("vid-1", NullLogger.Instance));

        fixture.Customize<PromptExecutionInfoDto>(c => c
            .With(p => p.Provider, "test")
            .With(p => p.Model, "m")
            .With(p => p.DurationMs, 1)
            .With(p => p.TokensPrompt, 1)
            .With(p => p.TokensCompletion, 1)
            .With(p => p.TokensTotal, 2)
            .With(p => p.RequestId, "rid")
            .With(p => p.CreatedAt, DateTimeOffset.UtcNow)
            .With(p => p.FinishReason, "stop")
        );

        // Factory method to start the fluent builder
        fixture.Register(() => new LlmResponseForProposalTests(fixture));

        // Shortcut factories to reduce public calls in tests
        fixture.Register((string type, string[] tags) => new Achievement(type, tags, null));
        fixture.Register((string domain, AchievementTypeGroup group, string justification) => new TaxonomyProposal(domain, group, justification));
    }
}
