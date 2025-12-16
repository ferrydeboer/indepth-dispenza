using System.ComponentModel.DataAnnotations;

namespace InDepthDispenza.Functions.Integrations.Azure.Storage;

public class StorageOptions
{
    /// <summary>
    /// Azure Storage connection string.
    /// </summary>
    [Required]
    public string? AzureWebJobsStorage { get; set; }

    /// <summary>
    /// Name of the Azure Storage queue for video processing.
    /// </summary>
    public string VideoQueueName { get; set; } = "videos";
}
