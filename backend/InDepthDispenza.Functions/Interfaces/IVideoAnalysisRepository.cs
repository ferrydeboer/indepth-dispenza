using InDepthDispenza.Functions.VideoAnalysis.Interfaces;

namespace InDepthDispenza.Functions.Interfaces;

public interface IVideoAnalysisRepository
{
    Task<ServiceResult> SaveAnalysisAsync(VideoAnalysisDocument document);
    Task<ServiceResult<VideoAnalysisDocument?>> GetAnalysisAsync(string videoId);
    Task<ServiceResult> SaveFullLlmResponseAsync(string id, DateTimeOffset analyzedAt, string? taxonomyVersion, LlmResponse llm);
}
