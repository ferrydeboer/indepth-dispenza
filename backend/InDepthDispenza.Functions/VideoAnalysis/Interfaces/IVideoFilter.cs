using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

public interface IVideoFilter
{
    Task<bool> ShouldProcessAsync(VideoInfo video, PlaylistScanRequest request);
}