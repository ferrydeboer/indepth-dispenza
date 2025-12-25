using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

/// <summary>
/// Handler that runs after a video has been analyzed to perform post-processing
/// tasks (e.g., taxonomy updates, persistence hooks, notifications).
/// Handlers receive the raw <see cref="LlmResponse"/> DTO so they can operate on
/// internal structures, while the public API returns <see cref="VideoAnalysis"/>.
/// Handlers are invoked in registration order and exceptions in one handler
/// must not prevent subsequent handlers from running.
/// </summary>
public interface IVideoAnalyzedHandler
{
    Task HandleAsync(LlmResponse response, VideosAnalyzedContext context);
}

/// <summary>
/// Execution context for <see cref="IVideoAnalyzedHandler"/>.
/// </summary>
public sealed class VideosAnalyzedContext
{
    public string VideoId { get; }
    public ILogger Logger { get; }

    public VideosAnalyzedContext(string videoId, ILogger logger)
    {
        VideoId = videoId;
        Logger = logger;
    }
}
