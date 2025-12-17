using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Prompting;

/// <summary>
/// Composes the taxonomy portion of the LLM prompt.
/// Loads taxonomy from repository and provides hierarchical structure for constrained tag extraction.
/// </summary>
public class TaxonomyPromptComposer : IPromptComposer
{
    private readonly ILogger<TaxonomyPromptComposer> _logger;
    private readonly ITaxonomyRepository _taxonomyRepository;
    private const string TemplateRelativePath = "VideoAnalysis/Prompting/taxonomy-prompt.md";

    // Fallback taxonomy if none exists in database
    private static readonly string FallbackTaxonomyJson = """
        {
          "taxonomy": {
            "healing": {
              "physical_health": {
                "subcategories": ["obesity", "high_blood_pressure", "acid_reflux", "insomnia"],
                "attributes": ["weight_loss", "chronic", "acute"]
              }
            },
            "manifestation": {
              "financial": {
                "subcategories": ["manifested_money"],
                "attributes": ["amount_over_10k"]
              }
            },
            "transformation": {
              "spiritual": {
                "subcategories": ["mystical_experience"],
                "attributes": ["insight"]
              }
            },
            "other": {}
          }
        }
        """;

    public TaxonomyPromptComposer(
        ILogger<TaxonomyPromptComposer> logger,
        ITaxonomyRepository taxonomyRepository)
    {
        _logger = logger;
        _taxonomyRepository = taxonomyRepository;
    }

    public async Task ComposeAsync(Prompt prompt, string videoId)
    {
        // Load taxonomy
        var taxonomyJson = await LoadTaxonomyJsonAsync();

        // Load template file
        var template = await LoadTemplateAsync();
        // Replace placeholder
        var filled = template.Replace("{taxonomy}", taxonomyJson);

        prompt.AddSegment(new PromptSegment(filled, Order: 10));
    }

    private async Task<string> LoadTaxonomyJsonAsync()
    {
        try
        {
            var result = await _taxonomyRepository.GetLatestTaxonomyAsync();

            if (!result.IsSuccess || result.Data == null)
            {
                _logger.LogWarning("Failed to load taxonomy from repository. Using fallback.");
                return FallbackTaxonomyJson;
            }

            // Serialize strong-typed taxonomy into the wrapped prompt shape { "taxonomy": ... }
            var doc = result.Data;
            var wrapper = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["taxonomy"] = new System.Collections.Generic.Dictionary<string, AchievementTypeGroup>(doc.Taxonomy)
            };
            return System.Text.Json.JsonSerializer.Serialize(wrapper, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading taxonomy. Using fallback.");
            return FallbackTaxonomyJson;
        }
    }

    private static async Task<string> LoadTemplateAsync()
    {
        try
        {
            // Try to load from output directory using AppContext.BaseDirectory
            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }

            // Fallback: try relative to current directory (useful for some test runners)
            var altPath = Path.Combine(Directory.GetCurrentDirectory(), TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(altPath))
            {
                return await File.ReadAllTextAsync(altPath);
            }

            // As a last resort, embed a minimal default template
            return "# Taxonomy for Tag Extraction\n```json\n{taxonomy}\n```";
        }
        catch
        {
            return "# Taxonomy for Tag Extraction\n```json\n{taxonomy}\n```";
        }
    }
}
