using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
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
    private readonly ITranscriptAnalyzer _transcriptAnalyzer;
    private readonly ITaxonomyUpdateService _taxonomyUpdateService;

    public AnalyzeVideo(ILogger<AnalyzeVideo> logger, ITranscriptAnalyzer transcriptAnalyzer, ITaxonomyUpdateService taxonomyUpdateService)
    {
        _logger = logger;
        _transcriptAnalyzer = transcriptAnalyzer;
        _taxonomyUpdateService = taxonomyUpdateService;
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

        // Delegate to business logic layer (TranscriptAnalyzer handles everything)
        var result = await _transcriptAnalyzer.AnalyzeTranscriptAsync(videoId);

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

        // Business orchestration (taxonomy update, persistence) is handled inside TranscriptAnalyzer
        // The Function only maps the result to HTTP response.
        return new OkObjectResult(new
        {
            videoId = analysis.Id,
            analyzedAt = analysis.AnalyzedAt,
            modelVersion = analysis.ModelVersion,
            achievements = analysis.Achievements,
            timeframe = analysis.Timeframe,
            practices = analysis.Practices,
            sentimentScore = analysis.SentimentScore,
            confidenceScore = analysis.ConfidenceScore,
            proposals = analysis.Proposals,
            message = "Video analysis complete."
        });
    }
}
