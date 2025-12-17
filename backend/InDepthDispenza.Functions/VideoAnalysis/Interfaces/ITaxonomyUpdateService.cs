using InDepthDispenza.Functions.Interfaces;
using VideoAnalysisModel = InDepthDispenza.Functions.VideoAnalysis.VideoAnalysis;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

/// <summary>
/// Handles updating the taxonomy based on proposals found in a video analysis.
/// </summary>
public interface ITaxonomyUpdateService
{
    /// <summary>
    /// If proposals are present in the analysis, merge them into the latest taxonomy
    /// and save a new version. Returns the new taxonomy version ID when an update
    /// occurred, or null if no proposals were present.
    /// </summary>
    Task<ServiceResult<string?>> ApplyProposalsAsync(VideoAnalysisModel analysis);
}
