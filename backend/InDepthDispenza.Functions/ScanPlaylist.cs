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
        try
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

            if (result.IsSuccess)
            {
                _logger.LogInformation("Playlist scan completed successfully. Videos processed: {Count}", result.Data);
                return new OkObjectResult(new ScanPlaylistResult(result.Data!));
            }
            else
            {
                _logger.LogError("Playlist scan failed: {Error}", result.ErrorMessage);
                return new BadRequestObjectResult(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ScanPlaylist function");
            return new StatusCodeResult(500);
        }
    }
}

public record ScanPlaylistResult(int VideosProcessed);