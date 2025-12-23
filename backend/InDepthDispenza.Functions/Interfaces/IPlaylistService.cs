
using System;

namespace InDepthDispenza.Functions.Interfaces;

public interface IPlaylistService
{
    IAsyncEnumerable<VideoInfo> GetPlaylistVideosAsync(string playlistId, int? limit = null, Func<VideoInfo, bool>? filter = null);
}