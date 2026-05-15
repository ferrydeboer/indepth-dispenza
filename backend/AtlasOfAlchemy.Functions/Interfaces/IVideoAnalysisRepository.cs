using AtlasOfAlchemy.Functions.VideoAnalysis.Interfaces;

namespace AtlasOfAlchemy.Functions.Interfaces;

public interface IVideoAnalysisRepository
{
    Task<ServiceResult> SaveAnalysisAsync(VideoAnalysisDocument document);
    Task<ServiceResult<VideoAnalysisDocument?>> GetAnalysisAsync(string videoId);
    Task<ServiceResult> SaveFullLlmResponseAsync(string id, DateTimeOffset analyzedAt, string? taxonomyVersion, string? versionLabel, LlmResponse llm);
    Task<int> GetAnalyzedVideoCountAsync();
}
