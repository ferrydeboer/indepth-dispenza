namespace InDepthDispenza.Functions.Interfaces;

public interface ITranscriptRepository
{
    Task<ServiceResult<TranscriptDocument?>> GetTranscriptAsync(string videoId);
    Task<ServiceResult> SaveTranscriptAsync(TranscriptDocument document);
}
