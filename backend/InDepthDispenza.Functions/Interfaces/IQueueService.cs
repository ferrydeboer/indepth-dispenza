namespace InDepthDispenza.Functions.Interfaces;

public interface IQueueService
{
    Task<ServiceResult> EnqueueVideoAsync(VideoInfo video);
}