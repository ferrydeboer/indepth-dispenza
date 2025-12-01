using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InDepthDispenza.Functions;

public class ScanPlaylist
{
    private readonly ILogger<ScanPlaylist> _logger;

    public ScanPlaylist(ILogger<ScanPlaylist> logger)
    {
        _logger = logger;
    }

    [Function("ScanPlaylist")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
        [FromQuery] int? limit)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult(new ScanPlaylistResult(limit ?? 0));
    }
}

public record ScanPlaylistResult(int OrchestrationCount)
{
}