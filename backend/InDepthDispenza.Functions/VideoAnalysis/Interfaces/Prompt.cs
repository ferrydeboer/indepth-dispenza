using System.Text;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

/// <summary>
/// Container for prompt segments composed by the pipeline.
/// </summary>
public class Prompt
{
    private readonly List<PromptSegment> _segments = new();

    /// <summary>
    /// Adds a segment to the prompt.
    /// </summary>
    public void AddSegment(PromptSegment segment)
    {
        _segments.Add(segment);
    }

    /// <summary>
    /// Builds the final prompt string from all segments.
    /// </summary>
    public string Build()
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Video Transcript Analysis Task");
        builder.AppendLine();
        builder.AppendLine("Analyze the following video transcript and extract structured healing journey data.");
        builder.AppendLine("Return your response as valid JSON matching the schema provided below.");
        builder.AppendLine();

        // Append all segments in order
        foreach (var segment in _segments)
        {
            builder.AppendLine(segment.Content);
            builder.AppendLine();
        }

        // Add output schema
        // builder.AppendLine("# Expected Output Schema");
        // builder.AppendLine();
        // builder.AppendLine("Return a JSON object with this structure:");
        // builder.AppendLine("```json");
        // builder.AppendLine("""
        //     {
        //       "modelVersion": "gpt-4o-mini",
        //       "achievements": [
        //         {
        //           "type": "healing | manifestation | transformation | other",
        //           "tags": ["array", "of", "snake_case_tags"],
        //           "details": "optional brief narrative"
        //         }
        //       ],
        //       "timeframe": {
        //         "noticeEffects": "time string (e.g., '2 weeks')",
        //         "fullHealing": "time string (e.g., '6 months')"
        //       },
        //       "practices": ["meditation", "breath_work", "workshops"],
        //       "sentimentScore": 0.85,
        //       "confidenceScore": 0.9,
        //       "proposals": [
        //         {
        //           "newTag": "proposed_new_tag",
        //           "parent": "parent_category",
        //           "justification": "why this tag is needed"
        //         }
        //       ]
        //     }
        //     """);
        // builder.AppendLine("```");
        // builder.AppendLine();
        // builder.AppendLine("**Important:** Return ONLY the JSON object, no additional text or explanation.");

        return builder.ToString();
    }
}

/// <summary>
/// A single segment of a prompt, contributed by a composer.
/// </summary>
/// <param name="Content">The text content of this segment</param>
/// <param name="Order">Optional ordering hint for sorting segments</param>
public record PromptSegment(string Content, int Order = 0);
