using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis;

[TestFixture]
public class TranscriptAnalyzerVersionedIdTests
{
    private Mock<ILlmService> _llmServiceMock;
    private Mock<IVideoAnalysisRepository> _repositoryMock;
    private TranscriptAnalyzer _testSubject;

    [SetUp]
    public void Setup()
    {
        _llmServiceMock = new Mock<ILlmService>();
        _repositoryMock = new Mock<IVideoAnalysisRepository>();

        _llmServiceMock.Setup(s => s.CallAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeMinimalSuccessResponse());

        _repositoryMock.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .ReturnsAsync(ServiceResult.Success());

        _testSubject = new TranscriptAnalyzer(
            NullLogger<TranscriptAnalyzer>.Instance,
            _llmServiceMock.Object,
            Array.Empty<IPromptComposer>(),
            Array.Empty<IVideoAnalyzedHandler>(),
            _repositoryMock.Object);
    }

    [Test]
    public async Task AnalyzeTranscriptAsync_WithVersionLabel_UsesVersionedDocumentId()
    {
        // Arrange
        var videoId = "test-video-123";
        var versionLabel = "v2.0";

        // Act
        var result = await _testSubject.AnalyzeTranscriptAsync(videoId, versionLabel);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        _repositoryMock.Verify(r => r.SaveFullLlmResponseAsync(
            It.Is<string>(id => id.StartsWith($"{videoId}_") && id.Contains("T") && id.EndsWith("Z")),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.Is<string?>(v => v == versionLabel),
            It.IsAny<LlmResponse>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeTranscriptAsync_WithoutVersionLabel_StillUsesVersionedDocumentId()
    {
        // Arrange
        var videoId = "test-video-456";

        // Act
        var result = await _testSubject.AnalyzeTranscriptAsync(videoId);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        _repositoryMock.Verify(r => r.SaveFullLlmResponseAsync(
            It.Is<string>(id => id.StartsWith($"{videoId}_") && id.Contains("T") && id.EndsWith("Z")),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.Is<string?>(v => v == null),
            It.IsAny<LlmResponse>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeTranscriptAsync_WithNullVersionLabel_StillUsesVersionedDocumentId()
    {
        // Arrange
        var videoId = "test-video-789";

        // Act
        var result = await _testSubject.AnalyzeTranscriptAsync(videoId, versionLabel: null);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        _repositoryMock.Verify(r => r.SaveFullLlmResponseAsync(
            It.Is<string>(id => id.StartsWith($"{videoId}_") && id.Contains("T") && id.EndsWith("Z")),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(),
            It.Is<string?>(v => v == null),
            It.IsAny<LlmResponse>()), Times.Once);
    }

    [Test]
    public async Task AnalyzeTranscriptAsync_VersionedId_CanBeParsedByVersionedDocumentId()
    {
        // Arrange
        var videoId = "dQw4w9WgXcQ";
        var versionLabel = "taxonomy-v3";
        string? capturedId = null;

        _repositoryMock.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .Callback<string, DateTimeOffset, string?, string?, LlmResponse>((id, _, _, _, _) => capturedId = id)
            .ReturnsAsync(ServiceResult.Success());

        // Act
        await _testSubject.AnalyzeTranscriptAsync(videoId, versionLabel);

        // Assert
        Assert.That(capturedId, Is.Not.Null);
        Assert.That(VersionedDocumentId.TryParse(capturedId!, out var parsed), Is.True);
        Assert.That(parsed.VideoId, Is.EqualTo(videoId));
    }

    private static ServiceResult<CommonLlmResponse> MakeMinimalSuccessResponse()
    {
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
}
