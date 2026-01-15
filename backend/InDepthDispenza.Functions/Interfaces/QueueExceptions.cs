namespace InDepthDispenza.Functions.Interfaces;

public abstract class QueueException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Thrown when a transient error occurs that might succeed if retried.
/// </summary>
public class QueueTransientException(string message, Exception? inner = null) : QueueException(message, inner);

/// <summary>
/// Thrown when a specific message cannot be enqueued (e.g. too large).
/// </summary>
public class QueueMessageException(string videoId, string message, Exception? inner = null) 
    : QueueException($"Video {videoId}: {message}", inner);

/// <summary>
/// Thrown when a critical configuration or security error occurs.
/// </summary>
public class QueueConfigurationException(string message, Exception? inner = null) : QueueException(message, inner);
