using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

/// <summary>
/// Transcript provider that caches results in Cosmos DB.
/// Decorator pattern: wraps another provider and adds caching.
/// </summary>
public class CosmosCachingTranscriptProvider : ITranscriptProvider
{
    private readonly ILogger<CosmosCachingTranscriptProvider> _logger;
    private readonly ITranscriptProvider _innerProvider;
    private readonly ITranscriptRepository _repository;

    public CosmosCachingTranscriptProvider(
        ILogger<CosmosCachingTranscriptProvider> logger,
        ITranscriptProvider innerProvider,
        ITranscriptRepository repository)
    {
        _logger = logger;
        _innerProvider = innerProvider;
        _repository = repository;
    }

    public async Task<ServiceResult<TranscriptDocument>> GetTranscriptAsync(string videoId, string[] preferredLanguages)
    {
        try
        {
            // Check cache first
            _logger.LogInformation("Checking cache for video {VideoId}", videoId);
            var cachedResult = await _repository.GetTranscriptAsync(videoId);

            if (!cachedResult.IsSuccess)
            {
                _logger.LogWarning("Cache check failed for video {VideoId}: {Error}",
                    videoId, cachedResult.ErrorMessage);
                // Continue to fetch from source
            }
            else if (cachedResult.Data != null)
            {
                _logger.LogInformation("Cache hit for video {VideoId}", videoId);
                // Return complete cached document with segments
                return ServiceResult<TranscriptDocument>.Success(cachedResult.Data);
            }

            // Cache miss - fetch from inner provider
            _logger.LogInformation("Cache miss for video {VideoId}, fetching from source", videoId);
            var fetchResult = await _innerProvider.GetTranscriptAsync(videoId, preferredLanguages);

            if (!fetchResult.IsSuccess || fetchResult.Data == null)
            {
                return fetchResult;
            }

            // Inner provider now returns TranscriptDocument, so we can save it directly
            // Save to cache (fire and forget - don't block on cache failures)
            _ = Task.Run(async () =>
            {
                try
                {
                    var saveResult = await _repository.SaveTranscriptAsync(fetchResult.Data);
                    if (!saveResult.IsSuccess)
                    {
                        _logger.LogWarning("Failed to cache transcript for video {VideoId}: {Error}",
                            videoId, saveResult.ErrorMessage);
                    }
                    else
                    {
                        _logger.LogInformation("Cached transcript for video {VideoId}", videoId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving transcript to cache for video {VideoId}", videoId);
                }
            });

            return fetchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in caching transcript provider for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument>.Failure($"Failed to get transcript: {ex.Message}", ex);
        }
    }
}
