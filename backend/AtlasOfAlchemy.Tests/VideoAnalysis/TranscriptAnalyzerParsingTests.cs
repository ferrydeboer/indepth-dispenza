using AtlasOfAlchemy.Functions.Interfaces;
using AtlasOfAlchemy.Functions.VideoAnalysis;
using AtlasOfAlchemy.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace AtlasOfAlchemy.Tests.VideoAnalysis;

/// <summary>
/// Tests for LLM response parsing and validation in TranscriptAnalyzer.
/// Focuses on the behavior when LLM returns invalid or empty responses.
/// </summary>
public class TranscriptAnalyzerParsingTests
{
    private Mock<ILlmService> _llmService = null!;
    private Mock<IVideoAnalysisRepository> _repository = null!;
    private TranscriptAnalyzer _testSubject = null!;

    [SetUp]
    public void SetUp()
    {
        _llmService = new Mock<ILlmService>();
        _repository = new Mock<IVideoAnalysisRepository>();
        _repository.Setup(r => r.SaveFullLlmResponseAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<LlmResponse>()))
            .ReturnsAsync(ServiceResult.Success());

        _testSubject = new TranscriptAnalyzer(
            NullLogger<TranscriptAnalyzer>.Instance,
            _llmService.Object,
            Array.Empty<IPromptComposer>(),
            Array.Empty<IVideoAnalyzedHandler>(),
            _repository.Object);
    }

    /// <summary>
    /// Reproduces issue #8: When LLM returns valid JSON with wrong structure (schema drift),
    /// the current code silently falls back to an empty analysis instead of throwing an exception.
    ///
    /// Expected behavior: Should throw LlmResponseException so the queue message is retried/poison-queued.
    /// Current behavior (bug): Silently persists empty analysis with no error.
    /// </summary>
    [Test]
    public async Task AnalyzeTranscript_WhenLlmReturnsWrongJsonStructure_ShouldThrowLlmResponseException()
    {
        // Arrange - LLM returns valid JSON but with completely wrong structure (schema drift)
        var wrongStructureJson = """
        {
          "completely_wrong_field": "value",
          "another_unexpected_field": 123
        }
        """;

        _llmService.Setup(s => s.CallAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeLlmResponse(wrongStructureJson));

        // Act & Assert - Should throw LlmResponseException, not silently succeed
        var ex = Assert.ThrowsAsync<LlmResponseException>(
            async () => await _testSubject.AnalyzeTranscriptAsync("video-123"));

        Assert.That(ex!.VideoId, Is.EqualTo("video-123"));
        Assert.That(ex.TruncatedResponse, Does.Contain("completely_wrong_field"));
    }

    /// <summary>
    /// Reproduces issue #8: When LLM returns JSON with null/empty analysis block,
    /// the current code silently persists an empty document.
    ///
    /// Expected behavior: Should throw LlmResponseException.
    /// </summary>
    [Test]
    public async Task AnalyzeTranscript_WhenLlmReturnsEmptyAnalysis_ShouldThrowLlmResponseException()
    {
        // Arrange - LLM returns correct structure but with all null/empty values
        var emptyAnalysisJson = """
        {
          "analysis": {
            "achievements": [],
            "timeframe": null,
            "practices": [],
            "sentimentScore": null,
            "confidenceScore": null
          },
          "proposals": { "taxonomy": [] }
        }
        """;

        _llmService.Setup(s => s.CallAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeLlmResponse(emptyAnalysisJson));

        // Act & Assert - Should throw LlmResponseException for empty analysis
        var ex = Assert.ThrowsAsync<LlmResponseException>(
            async () => await _testSubject.AnalyzeTranscriptAsync("video-123"));

        Assert.That(ex!.VideoId, Is.EqualTo("video-123"));
        Assert.That(ex.Message, Does.Contain("no meaningful analysis"));
    }

    /// <summary>
    /// Reproduces issue #8: When LLM returns JSON with null analysis block,
    /// should throw LlmResponseException.
    /// </summary>
    [Test]
    public async Task AnalyzeTranscript_WhenLlmReturnsNullAnalysis_ShouldThrowLlmResponseException()
    {
        // Arrange - analysis is explicitly null
        var nullAnalysisJson = """
        {
          "analysis": null,
          "proposals": { "taxonomy": [] }
        }
        """;

        _llmService.Setup(s => s.CallAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeLlmResponse(nullAnalysisJson));

        // Act & Assert
        var ex = Assert.ThrowsAsync<LlmResponseException>(
            async () => await _testSubject.AnalyzeTranscriptAsync("video-123"));

        Assert.That(ex!.VideoId, Is.EqualTo("video-123"));
        Assert.That(ex.Message, Does.Contain("no meaningful analysis"));
    }

    /// <summary>
    /// Valid response with achievements should succeed (baseline test).
    /// </summary>
    [Test]
    public async Task AnalyzeTranscript_WhenLlmReturnsValidResponse_ShouldSucceed()
    {
        // Arrange
        var validJson = """
        {
          "analysis": {
            "achievements": [
              { "type": "healing", "tags": ["physical_healing"], "details": "Recovered from illness" }
            ],
            "timeframe": { "noticeEffects": "2 weeks", "fullHealing": "3 months" },
            "practices": ["meditation"],
            "sentimentScore": 0.8,
            "confidenceScore": 0.9
          },
          "proposals": { "taxonomy": [] }
        }
        """;

        _llmService.Setup(s => s.CallAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeLlmResponse(validJson));

        // Act
        var result = await _testSubject.AnalyzeTranscriptAsync("video-123");

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Data!.Achievements, Has.Length.EqualTo(1));
    }

    private static ServiceResult<CommonLlmResponse> MakeLlmResponse(string jsonContent)
    {
        var assistant = new LlmAssistantPayload(jsonContent, "application/json", null);
        var call = new LlmCallInfo("test-provider", "test-model", 100, 50, 50, 100, "req-123", DateTimeOffset.UtcNow, "stop");
        var response = new CommonLlmResponse(call, assistant);
        return ServiceResult<CommonLlmResponse>.Success(response);
    }
}
