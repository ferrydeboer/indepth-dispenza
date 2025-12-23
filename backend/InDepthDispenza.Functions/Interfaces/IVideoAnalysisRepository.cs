namespace InDepthDispenza.Functions.Interfaces;

public interface IVideoAnalysisRepository
{
    Task<ServiceResult> SaveAnalysisAsync(VideoAnalysisDocument document);
    Task<ServiceResult<VideoAnalysisDocument?>> GetAnalysisAsync(string videoId);
    Task<ServiceResult> SaveFullLlmResponseAsync(string id, DateTimeOffset analyzedAt, string? taxonomyVersion, InDepthDispenza.Functions.VideoAnalysis.Interfaces.LlmResponse llm);
}
