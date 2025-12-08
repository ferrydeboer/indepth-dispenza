using System.ComponentModel.DataAnnotations;

namespace InDepthDispenza.Functions.Integrations.YouTube;

public class YouTubeOptions
{
    [Required]
    public string? ApiKey { get; set; }
    public string? ApiBaseUrl { get; set; }
}
