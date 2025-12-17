using System.Text.Json;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Service for making LLM API calls.
/// Thin adapter around LLM APIs - does not handle prompt composition.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Calls the LLM with a composed prompt.
    /// </summary>
    /// <param name="prompt">The complete prompt to send to the LLM</param>
    /// <returns>The LLM's response as a JSON document</returns>
    Task<ServiceResult<CommonLlmResponse>> CallAsync(string prompt);
}
