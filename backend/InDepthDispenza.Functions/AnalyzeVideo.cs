using InDepthDispenza.Functions.VideoAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions;

/// <summary>
/// Azure Function entrypoint for video analysis.
/// Contains only HTTP-related concerns (parsing, validation, response mapping).
/// All business logic is delegated to TranscriptAnalyzer.
/// </summary>
public class AnalyzeVideo
{
    private readonly ILogger<AnalyzeVideo> _logger;
    private readonly TranscriptAnalyzer _transcriptAnalyzer;

    public AnalyzeVideo(ILogger<AnalyzeVideo> logger, TranscriptAnalyzer transcriptAnalyzer)
    {
        _logger = logger;
        _transcriptAnalyzer = transcriptAnalyzer;
    }

    [Function("AnalyzeVideo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("AnalyzeVideo function triggered");

        // HTTP concern: Parse and validate request
        var videoId = req.Query["videoId"].ToString();
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return new BadRequestObjectResult("Missing required parameter: videoId");
        }

        // Delegate to business logic layer
        var result = await _transcriptAnalyzer.AnalyzeVideoAsync(videoId);

        // HTTP concern: Map business result to HTTP response
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to analyze video: {Error}", result.ErrorMessage);
            return new ObjectResult(new { error = result.ErrorMessage })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        var analysis = result.Data!;
        return new OkObjectResult(new
        {
            videoId = analysis.VideoId,
            title = analysis.Title,
            transcriptLength = analysis.CharacterCount,
            wordCount = analysis.WordCount,
            language = analysis.Language,
            isEmpty = analysis.IsEmpty,
            readyForLlm = analysis.ReadyForLlm,
            message = "Video analysis complete. Ready for LLM processing."
        });
    }
}
