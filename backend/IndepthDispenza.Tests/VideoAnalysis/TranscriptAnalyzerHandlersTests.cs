using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
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
        public Task HandleAsync(LlmResponse response, VideosAnalyzedContext context)
        {
            _calls.Add(_name);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IVideoAnalyzedHandler
    {
        public Task HandleAsync(LlmResponse response, InDepthDispenza.Functions.VideoAnalysis.Interfaces.VideosAnalyzedContext context)
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
        taxonomyUpdate.Setup(t => t.ApplyProposalsAsync(It.IsAny<string>(), It.IsAny<TaxonomyProposal[]>()))
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
}
