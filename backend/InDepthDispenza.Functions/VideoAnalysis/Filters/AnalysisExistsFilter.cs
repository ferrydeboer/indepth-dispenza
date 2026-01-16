using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Filters;

public class AnalysisExistsFilter : IVideoFilter
{
    private readonly IVideoAnalysisRepository _repository;
    private readonly ILogger<AnalysisExistsFilter> _logger;

    public AnalysisExistsFilter(IVideoAnalysisRepository repository, ILogger<AnalysisExistsFilter> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> ShouldProcessAsync(VideoInfo video, PlaylistScanRequest request)
    {
        if (request.Filters == null || !request.Filters.SkipExisting)
        {
            return true;
        }

        try
        {
            var result = await _repository.GetAnalysisAsync(video.VideoId);
            if (result.IsSuccess && result.Data != null)
            {
                _logger.LogInformation("Skipping video {VideoId} as analysis already exists.", video.VideoId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for video {VideoId}. Skipping video due to filter error.", video.VideoId);
            return false;
        }

        return true;
    }
}