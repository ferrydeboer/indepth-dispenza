
namespace InDepthDispenza.Functions.Interfaces;

public interface IPlaylistService
{
    Task<ServiceResult<IEnumerable<VideoInfo>>> GetPlaylistVideosAsync(string playlistId, int? limit = null);
}