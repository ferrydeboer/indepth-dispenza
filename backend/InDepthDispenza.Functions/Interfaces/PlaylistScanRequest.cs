namespace InDepthDispenza.Functions.Interfaces;

public record PlaylistScanRequest(
    string PlaylistId,
    int? Limit = null,
    VideoFilters? Filters = null);