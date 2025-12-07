namespace InDepthDispenza.Functions.Interfaces;

public interface IPlaylistScanService
{
    Task<ServiceResult<int>> ScanPlaylistAsync(PlaylistScanRequest request);
}