using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Taxonomy;

/// <summary>
/// Applies taxonomy proposals from analysis and updates taxonomy version in context.
/// </summary>
public sealed class TaxonomyProposalUpdateHandler : IVideoAnalyzedHandler
{
    private readonly ITaxonomyUpdateService _taxonomyUpdateService;
    private readonly ILogger<TaxonomyProposalUpdateHandler> _logger;

    public TaxonomyProposalUpdateHandler(
        ITaxonomyUpdateService taxonomyUpdateService,
        ILogger<TaxonomyProposalUpdateHandler> logger)
    {
        _taxonomyUpdateService = taxonomyUpdateService;
        _logger = logger;
    }

    public async Task HandleAsync(VideoAnalysis analysis, VideosAnalyzedContext context)
    {
        // Skip if no proposals
        if (analysis.Proposals is null || analysis.Proposals.Length == 0)
        {
            return;
        }

        var update = await _taxonomyUpdateService.ApplyProposalsAsync(analysis);
        if (!update.IsSuccess)
        {
            _logger.LogWarning("Taxonomy update from proposals failed: {Error}", update.ErrorMessage);
        }
    }
}
