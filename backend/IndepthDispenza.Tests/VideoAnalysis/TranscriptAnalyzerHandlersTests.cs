using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Taxonomy;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis;

public class TranscriptAnalyzerHandlersTests
{
    private static ServiceResult<CommonLlmResponse> MakeMinimalSuccessResponse()
    {
        // Minimal valid response matching LlmVideoAnalysisResponseDto
        var json = """
        {
          "analysis": {
            "achievements": [
              { "type": "healing", "tags": ["tag1"], "details": "d" }
            ],
            "timeframe": { "noticeEffects": null, "fullHealing": null },
            "practices": ["p1"],
            "sentimentScore": 0.5,
            "confidenceScore": 0.8
          },
          "proposals": { "taxonomy": [] }
        }
        """;

        var assistant = new LlmAssistantPayload(json, "application/json", null);
        var call = new LlmCallInfo("test", "model-x", 10, 1, 1, 2, "rid", DateTimeOffset.UtcNow, "stop");
        var resp = new CommonLlmResponse(call, assistant);
        return ServiceResult<CommonLlmResponse>.Success(resp);
    }

    private sealed class RecordingHandler : IVideoAnalyzedHandler
    {
        private readonly List<string> _calls;
        private readonly string _name;
        public RecordingHandler(List<string> calls, string name) { _calls = calls; _name = name; }
        public Task HandleAsync(InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis analysis, VideosAnalyzedContext context)
        {
            _calls.Add(_name);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IVideoAnalyzedHandler
    {
        public Task HandleAsync(InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis analysis, InDepthDispenza.Functions.VideoAnalysis.Interfaces.VideosAnalyzedContext context)
        {
            throw new InvalidOperationException("boom");
        }
    }

    [Test]
    public async Task AnalyzeTranscript_CallsHandlersInOrder_AndContinuesOnException()
    {
        // Arrange
        var llm = new Mock<ILlmService>();
        llm.Setup(s => s.CallAsync(It.IsAny<string>()))
           .ReturnsAsync(MakeMinimalSuccessResponse());

        var repo = new Mock<IVideoAnalysisRepository>();
        repo.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .ReturnsAsync(ServiceResult.Success());

        var taxonomyUpdate = new Mock<ITaxonomyUpdateService>();
        taxonomyUpdate.Setup(t => t.ApplyProposalsAsync(It.IsAny<InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis>()))
            .ReturnsAsync(ServiceResult<string?>.Success(null));

        var calls = new List<string>();
        var handlers = new IVideoAnalyzedHandler[]
        {
            new RecordingHandler(calls, "h1"),
            new ThrowingHandler(),
            new RecordingHandler(calls, "h2"),
        };

        var sut = new TranscriptAnalyzer(
            NullLogger<TranscriptAnalyzer>.Instance,
            llm.Object,
            Array.Empty<IPromptComposer>(),
            handlers,
            repo.Object);

        // Act
        var result = await sut.AnalyzeTranscriptAsync("vid-123");

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
        Assert.That(calls, Is.EqualTo(new[] { "h1", "h2" }), "Handlers should be called in order and continue after exception");

        repo.Verify(r => r.SaveFullLlmResponseAsync(
            It.Is<string>(id => id == "vid-123"),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.IsAny<LlmResponse>()), Times.Once);
    }

    [Test]
    public async Task TaxonomyProposalUpdateHandler_SetsTaxonomyVersion_WhenUpdateSucceeds()
    {
        // Arrange: response with one proposal
        var json = """
        {
          "analysis": {
            "achievements": [],
            "timeframe": null,
            "practices": [],
            "sentimentScore": 0.0,
            "confidenceScore": 0.0
          },
          "proposals": { "taxonomy": [ { "healing": { "new_cat": { "subcategories": [], "attributes": [] } }, "justification": "because" } ] }
        }
        """;
        var assistant = new LlmAssistantPayload(json, "application/json", null);
        var call = new LlmCallInfo("test", "model-x", 10, 1, 1, 2);
        var resp = new CommonLlmResponse(call, assistant);

        var llm = new Mock<ILlmService>();
        llm.Setup(s => s.CallAsync(It.IsAny<string>()))
           .ReturnsAsync(ServiceResult<CommonLlmResponse>.Success(resp));

        var repo = new Mock<IVideoAnalysisRepository>();
        repo.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .ReturnsAsync(ServiceResult.Success());

        var taxonomyUpdate = new Mock<ITaxonomyUpdateService>();
        taxonomyUpdate.Setup(t => t.ApplyProposalsAsync(It.IsAny<InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis>()))
            .ReturnsAsync(ServiceResult<string?>.Success("v1.2"));

        string? observedVersion = null;
        var taxonomyHandler = new TaxonomyProposalUpdateHandler(taxonomyUpdate.Object, NullLogger<TaxonomyProposalUpdateHandler>.Instance);

        var handlers = new IVideoAnalyzedHandler[] { taxonomyHandler };

        var sut = new TranscriptAnalyzer(
            NullLogger<TranscriptAnalyzer>.Instance,
            llm.Object,
            Array.Empty<IPromptComposer>(),
            handlers,
            repo.Object);

        // Act
        var result = await sut.AnalyzeTranscriptAsync("vid-456");

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
        Assert.That(sut.GetLastTaxonomyVersion(), Is.EqualTo("v1.2"));
    }

    private sealed class CallbackHandler : IVideoAnalyzedHandler
    {
        private readonly Action<InDepthDispenza.Functions.VideoAnalysis.Interfaces.VideosAnalyzedContext> _callback;
        public CallbackHandler(Action<InDepthDispenza.Functions.VideoAnalysis.Interfaces.VideosAnalyzedContext> callback) { _callback = callback; }
        public Task HandleAsync(InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis analysis, InDepthDispenza.Functions.VideoAnalysis.Interfaces.VideosAnalyzedContext context)
        {
            _callback(context);
            return Task.CompletedTask;
        }
    }
}
