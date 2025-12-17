using System.Text.Json;
using System.Text.Json.Serialization;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.VideoAnalysis.Prompting;

/// <summary>
/// Composes the output format section of the LLM prompt.
/// It serializes an example instance of LlmResponseDto (and below) and injects it into the output template.
/// </summary>
public class OutputPromptComposer : IPromptComposer
{
    private readonly ILogger<OutputPromptComposer> _logger;
    private const string TemplateRelativePath = "VideoAnalysis/Prompting/output-prompt.md";

    public OutputPromptComposer(ILogger<OutputPromptComposer> logger)
    {
        _logger = logger;
    }

    public async Task ComposeAsync(Prompt prompt, string videoId)
    {
        // Prepare example payload using LlmResponseDto and models below it only
        var example = BuildExampleResponse();

        var json = JsonSerializer.Serialize(example, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });

        var template = await LoadTemplateAsync();
        var filled = template.Replace("{format}", json);

        prompt.AddSegment(new PromptSegment(filled, Order: 90));
    }

    private static async Task<string> LoadTemplateAsync()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }

            var altPath = Path.Combine(Directory.GetCurrentDirectory(), TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(altPath))
            {
                return await File.ReadAllTextAsync(altPath);
            }

            return "# Expected Output Schema\n```json\n{format}\n```\n";
        }
        catch (Exception ex)
        {
            // Last resort minimal template
            return "# Expected Output Schema\n```json\n{format}\n```\n";
        }
    }

    private static LlmVideoAnalysisResponseDto BuildExampleResponse()
    {
        // The example should use realistic tags from the taxonomy seed/fallback shown elsewhere in the prompts
        var achievements = new[]
        {
            new Achievement(
                Type: "healing",
                Tags: new[] { "healing", "physical_health", "obesity", "weight_loss" },
                Details: "Lost 25 pounds and normalized blood pressure after 10 weeks of consistent practice."),
            new Achievement(
                Type: "manifestation",
                Tags: new[] { "manifestation", "financial", "manifested_money", "amount_over_10k" },
                Details: "Unexpected $15,000 business opportunity after shifting beliefs and daily meditations.")
        };

        var practices = new[] { "meditation", "breath_work", "journaling" };

        // Example proposal demonstrating the expected structure when a new tag is truly needed
        var proposalSuggestion = new AchievementTypeGroup
        {
            ["neurological"] = new CategoryNode
            {
                Subcategories = new List<string> { "tinnitus" },
                Attributes = new List<string>()
            }
        };

        var proposals = new[]
        {
            new TaxonomyProposal(
                "healing", proposalSuggestion,
                "Multiple testimonials mention tinnitus improvements; proposing a 'neurological' subcategory with 'tinnitus' for better specificity.")
        };

        return new LlmVideoAnalysisResponseDto(
            Analysis: new AnalysisDto(
                Achievements: achievements,
                Timeframe: new Timeframe(
                    NoticeEffects: "2 weeks",
                    FullHealing: "3 months"
                ),
                Practices: practices,
                SentimentScore: 0.78,
                ConfidenceScore: 0.82
            ),
            Proposals: new ProposalsDto(
                Taxonomy: proposals
            )
        );
    }
}