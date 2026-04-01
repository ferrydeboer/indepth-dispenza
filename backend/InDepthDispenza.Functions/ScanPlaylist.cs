using InDepthDispenza.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InDepthDispenza.Functions;

public class ScanPlaylist
{
    private readonly ILogger<ScanPlaylist> _logger;
    private readonly IPlaylistScanService _playlistScanService;
    private readonly IVideoAnalysisRepository _videoAnalysisRepository;

    public ScanPlaylist(
        ILogger<ScanPlaylist> logger, 
        IPlaylistScanService playlistScanService,
        IVideoAnalysisRepository videoAnalysisRepository)
    {
        _logger = logger;
        _playlistScanService = playlistScanService;
        _videoAnalysisRepository = videoAnalysisRepository;
    }

    [Function("ScanPlaylist")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        [FromQuery] string? playlistId,
        [FromQuery] int? limit,
        [FromQuery] string? filters,
        [FromQuery] string? version = null)
    {
        _logger.LogInformation("ScanPlaylist function triggered with playlistId: {PlaylistId}, limit: {Limit}, filters: {Filters}, version: {Version}",
            playlistId, limit, filters, version);

        // Validate input
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            _logger.LogWarning("Missing or empty playlistId parameter");
            return new BadRequestObjectResult(new { error = "PlaylistId parameter is required" });
        }

        // Create request
        var request = new PlaylistScanRequest(playlistId, limit, VideoFilters.Parse(filters), version);

        // Execute scan
        var result = await _playlistScanService.ScanPlaylistAsync(request);

        _logger.LogInformation("Playlist scan completed successfully. Videos processed: {Count}", result.Data);
        return new OkObjectResult(new ScanPlaylistResult(result.Data));
    }

    [Function("ScheduledPlaylistScan")]
    public async Task ScheduledRun([TimerTrigger("0 0 10 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("ScheduledPlaylistScan triggered at: {Time}", DateTime.UtcNow);

        const string hardcodedPlaylistId = "PLD4EAA8F8C9148A1B";
        
        // Count analyzed videos
        var analyzedCount = await _videoAnalysisRepository.GetAnalyzedVideoCountAsync();
        // This limits the total but this does not guarantee a total given the skip-existing filter.
        // Hence it's not higher than the 25 transcripts allowed.
        var limit = analyzedCount + 50;
        
        _logger.LogInformation("Analyzed videos count: {AnalyzedCount}, setting limit to: {Limit}", analyzedCount, limit);

        // Create request with skip-existing filter
        var request = new PlaylistScanRequest(hardcodedPlaylistId, limit, VideoFilters.Parse("skip-existing"));

        // Execute scan
        var result = await _playlistScanService.ScanPlaylistAsync(request);

        _logger.LogInformation("Scheduled playlist scan completed. Videos processed: {Count}", result.Data);
    }
}

public record ScanPlaylistResult(int VideosProcessed);