namespace AtlasOfAlchemy.Functions.VideoAnalysis;

/// <summary>
/// Exception thrown when LLM response cannot be parsed into a valid analysis.
/// This allows Azure Functions to retry the message or route to poison queue.
/// </summary>
public class LlmResponseException : Exception
{
    public string VideoId { get; }
    public string? TruncatedResponse { get; }

    public LlmResponseException(string videoId, string message, string? truncatedResponse = null)
        : base(message)
    {
        VideoId = videoId;
        TruncatedResponse = truncatedResponse;
    }

    public LlmResponseException(string videoId, string message, Exception innerException, string? truncatedResponse = null)
        : base(message, innerException)
    {
        VideoId = videoId;
        TruncatedResponse = truncatedResponse;
    }
}
