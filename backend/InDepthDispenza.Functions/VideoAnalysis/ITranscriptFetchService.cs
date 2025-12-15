using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis;

public interface ITranscriptFetchService
{
    Task<ServiceResult<TranscriptDocument>> GetOrFetchTranscriptAsync(VideoInfo videoInfo, string[] preferredLanguages);
}
