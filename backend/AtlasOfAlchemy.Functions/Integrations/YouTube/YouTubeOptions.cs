using System.ComponentModel.DataAnnotations;

namespace AtlasOfAlchemy.Functions.Integrations.YouTube;

public class YouTubeOptions
{
    [Required]
    public string? ApiKey { get; set; }
    public string? ApiBaseUrl { get; set; }
}
