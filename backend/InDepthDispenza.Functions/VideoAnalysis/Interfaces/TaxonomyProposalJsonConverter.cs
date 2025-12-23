using System.Text.Json;
using System.Text.Json.Serialization;
using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

internal sealed class TaxonomyProposalJsonConverter : JsonConverter<TaxonomyProposal>
{
    public override TaxonomyProposal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for TaxonomyProposal");
        }

        string? justification = null;
        string? achievementCategory = null;
        AchievementTypeGroup? group = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in TaxonomyProposal");
            }

            var propName = reader.GetString()!;
            reader.Read(); // move to value

            if (string.Equals(propName, "justification", StringComparison.OrdinalIgnoreCase))
            {
                justification = reader.TokenType == JsonTokenType.String ? reader.GetString() : JsonSerializer.Deserialize<string>(ref reader, options);
                continue;
            }

            // Otherwise, treat property name as the dynamic parent key
            achievementCategory = propName;
            group = JsonSerializer.Deserialize<AchievementTypeGroup>(ref reader, options) ?? new AchievementTypeGroup();
        }

        if (achievementCategory is null || group is null)
        {
            // No dynamic key present; fall back to an empty placeholder
            return new TaxonomyProposal("unspecified", new AchievementTypeGroup(), justification ?? string.Empty);
        }

        return new TaxonomyProposal(achievementCategory, group, justification ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, TaxonomyProposal value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // dynamic parent property using AchievementCategory
        writer.WritePropertyName(value.AchievementCategory);
        JsonSerializer.Serialize(writer, value.Group, options);

        // justification as sibling property
        writer.WriteString("justification", value.Justification);

        writer.WriteEndObject();
    }
}