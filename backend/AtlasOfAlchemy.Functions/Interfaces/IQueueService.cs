namespace AtlasOfAlchemy.Functions.Interfaces;

public interface IQueueService
{
    Task EnqueueVideoAsync(VideoInfo video);
}