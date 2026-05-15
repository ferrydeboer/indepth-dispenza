using AtlasOfAlchemy.Functions.Interfaces;

namespace AtlasOfAlchemy.Functions.VideoAnalysis.Interfaces;

public interface IVideoFilter
{
    Task<bool> ShouldProcessAsync(VideoInfo video, PlaylistScanRequest request);
}