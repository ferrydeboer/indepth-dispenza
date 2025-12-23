using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using InDepthDispenza.Functions.Interfaces;

namespace InDepthDispenza.Functions.VideoAnalysis.Interfaces;

internal sealed class NewtonsoftTaxonomyProposalConverter : JsonConverter<TaxonomyProposal>
{
    public override void WriteJson(JsonWriter writer, TaxonomyProposal? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(value.AchievementCategory);
        serializer.Serialize(writer, value.Group);
        writer.WritePropertyName("justification");
        writer.WriteValue(value.Justification);
        writer.WriteEndObject();
    }

    public override TaxonomyProposal? ReadJson(JsonReader reader, Type objectType, TaxonomyProposal? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader);

        // justification (case-insensitive)
        var justificationToken = obj.GetValue("justification", StringComparison.OrdinalIgnoreCase);
        var justification = justificationToken?.Type == JTokenType.String ? (string?)justificationToken : justificationToken?.ToObject<string>(serializer);

        // Find the dynamic domain key (any property that's not justification)
        JProperty? dynamicProp = null;
        foreach (var prop in obj.Properties())
        {
            if (!string.Equals(prop.Name, "justification", StringComparison.OrdinalIgnoreCase))
            {
                dynamicProp = prop;
                break;
            }
        }

        if (dynamicProp is null)
        {
            return new TaxonomyProposal("unspecified", new AchievementTypeGroup(), justification ?? string.Empty);
        }

        var achievementCategory = dynamicProp.Name;
        var group = dynamicProp.Value.ToObject<AchievementTypeGroup>(serializer) ?? new AchievementTypeGroup();

        return new TaxonomyProposal(achievementCategory, group, justification ?? string.Empty);
    }
}
