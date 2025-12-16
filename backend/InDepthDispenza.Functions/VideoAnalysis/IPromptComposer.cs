namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Composes part of an LLM prompt by adding a segment to the prompt.
/// Each composer is self-contained and loads its own dependencies.
/// </summary>
public interface IPromptComposer
{
    /// <summary>
    /// Adds this composer's segment to the prompt.
    /// Composer loads any data it needs (taxonomy, transcript, etc.).
    /// </summary>
    /// <param name="prompt">The prompt to add a segment to</param>
    /// <param name="videoId">The video ID being analyzed</param>
    Task ComposeAsync(Prompt prompt, string videoId);
}
