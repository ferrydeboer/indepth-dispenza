using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

/// <summary>
/// Analyzes video transcripts by orchestrating prompt composition and LLM calls.
/// </summary>
public interface ITranscriptAnalyzer
{
    /// <summary>
    /// Analyzes a transcript to extract healing journey data.
    /// Composers in the pipeline load transcript and taxonomy data as needed.
    /// </summary>
    /// <param name="videoId">The video ID being analyzed</param>
    /// <returns>Analysis result containing structured data</returns>
    Task<ServiceResult<VideoAnalysis>> AnalyzeTranscriptAsync(string videoId);
}
