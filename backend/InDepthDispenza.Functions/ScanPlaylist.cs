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

    public ScanPlaylist(ILogger<ScanPlaylist> logger, IPlaylistScanService playlistScanService)
    {
        _logger = logger;
        _playlistScanService = playlistScanService;
    }

    [Function("ScanPlaylist")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        [FromQuery] string? playlistId,
        [FromQuery] int? limit)
    {
        _logger.LogInformation("ScanPlaylist function triggered with playlistId: {PlaylistId}, limit: {Limit}", 
            playlistId, limit);

        // Validate input
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            _logger.LogWarning("Missing or empty playlistId parameter");
            return new BadRequestObjectResult(new { error = "PlaylistId parameter is required" });
        }

        // Create request
        var request = new PlaylistScanRequest(playlistId, limit);

        // Execute scan
        var result = await _playlistScanService.ScanPlaylistAsync(request);

        _logger.LogInformation("Playlist scan completed successfully. Videos processed: {Count}", result.Data);
        return new OkObjectResult(new ScanPlaylistResult(result.Data));
    }
}

public record ScanPlaylistResult(int VideosProcessed);