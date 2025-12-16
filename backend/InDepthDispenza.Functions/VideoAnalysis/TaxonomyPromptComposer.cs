using System.Text;
using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis;

/// <summary>
/// Composes the taxonomy portion of the LLM prompt.
/// Loads taxonomy from repository and provides hierarchical structure for constrained tag extraction.
/// </summary>
public class TaxonomyPromptComposer : IPromptComposer
{
    private readonly ILogger<TaxonomyPromptComposer> _logger;
    private readonly ITaxonomyRepository _taxonomyRepository;

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

        var content = new StringBuilder();
        content.AppendLine("# Taxonomy for Tag Extraction");
        content.AppendLine();
        content.AppendLine("Use the following taxonomy to extract structured tags from the transcript.");
        content.AppendLine("Tags MUST come from this taxonomy. Only propose new tags if the content clearly doesn't fit existing categories.");
        content.AppendLine();
        content.AppendLine("```json");
        content.AppendLine(taxonomyJson);
        content.AppendLine("```");
        content.AppendLine();
        content.AppendLine("**Taxonomy Rules:**");
        content.AppendLine("- Use snake_case for all tags (e.g., 'cervical_cancer', not 'Cervical Cancer')");
        content.AppendLine("- Include parent categories when relevant (e.g., both 'cancer' and 'lung_cancer')");
        content.AppendLine("- If proposing new tags, provide clear justification in the 'proposals' array");

        prompt.AddSegment(new PromptSegment(content.ToString(), Order: 10));
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

            return result.Data.Taxonomy.RootElement.GetRawText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading taxonomy. Using fallback.");
            return FallbackTaxonomyJson;
        }
    }
}
