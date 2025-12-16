namespace InDepthDispenza.Functions.Integrations.Azure.OpenAI;

public sealed class OpenAIOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL (e.g., https://my-resource.openai.azure.com/)
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Deployment name for the model (e.g., gpt-4o-mini)
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Optional: Model version to use (defaults to what's configured in deployment)
    /// </summary>
    public string? ModelVersion { get; set; }

    /// <summary>
    /// Optional: Maximum tokens for completion (defaults to 4000)
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Optional: Temperature for response generation (0.0 to 1.0, defaults to 0.7)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
