namespace InDepthDispenza.Functions.Integrations.XAi.Grok;

public sealed class GrokOptions
{
    /// <summary>
    /// Whether to enable the Grok integration. Keep false by default to avoid accidental API calls during tests/CI.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// xAI API base URL (OpenAI-compatible). Example: https://api.x.ai/v1
    /// </summary>
    public string? BaseUrl { get; set; } = "https://api.x.ai/v1";

    /// <summary>
    /// API key for xAI.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model name to use (e.g., grok-2-latest).
    /// </summary>
    public string? Model { get; set; } = "grok-2-latest";

    /// <summary>
    /// Maximum tokens for completion.
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for response generation (0.0 - 1.0).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Request timeout in seconds for the HTTP client.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
