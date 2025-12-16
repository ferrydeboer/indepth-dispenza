using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.Integrations.Azure;

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

    public async Task<ServiceResult<TranscriptData>> GetTranscriptAsync(string videoId, string[] preferredLanguages)
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
                // Note: Cached data doesn't include segments or metadata - those are lost in caching
                // This is acceptable as the cache is primarily for the text content
                return ServiceResult<TranscriptData>.Success(
                    new TranscriptData(
                        cachedResult.Data.Transcript ?? string.Empty,
                        cachedResult.Data.Language,
                        Array.Empty<TranscriptSegment>()));
            }

            // Cache miss - fetch from inner provider
            _logger.LogInformation("Cache miss for video {VideoId}, fetching from source", videoId);
            var fetchResult = await _innerProvider.GetTranscriptAsync(videoId, preferredLanguages);

            if (!fetchResult.IsSuccess || fetchResult.Data == null)
            {
                return fetchResult;
            }

            // Save to cache (fire and forget - don't block on cache failures)
            _ = Task.Run(async () =>
            {
                try
                {
                    var document = new TranscriptDocument(
                        Id: videoId,
                        Transcript: fetchResult.Data.Text,
                        Language: fetchResult.Data.Language,
                        FetchedAt: DateTimeOffset.UtcNow,
                        VideoTitle: fetchResult.Data?.Metadata?.Title ?? string.Empty,
                        VideoDescription: fetchResult.Data?.Metadata?.Description ?? string.Empty,
                        Duration: fetchResult.Data?.Metadata?.LengthSeconds ?? 0
                    );

                    var saveResult = await _repository.SaveTranscriptAsync(document);
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
            return ServiceResult<TranscriptData>.Failure($"Failed to get transcript: {ex.Message}", ex);
        }
    }
}
