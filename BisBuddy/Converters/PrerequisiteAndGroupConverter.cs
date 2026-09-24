using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteAndGroupConverter : JsonConverter<PrerequisiteAndGroup>
    {
        public override PrerequisiteAndGroup? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected StartObject for {nameof(PrerequisiteAndGroup)}");

            bool? isActive = null;
            PrerequisiteNodeSourceType? sourceType = null;
            List<PrerequisiteNode>? prerequisites = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(PrerequisiteAndGroup.IsActive):
                        isActive = reader.GetBoolean();
                        break;
                    case nameof(PrerequisiteAndGroup.SourceType):
                        sourceType = (PrerequisiteNodeSourceType)reader.GetInt32();
                        break;
                    case nameof(PrerequisiteAndGroup.Prerequisites):
                        prerequisites = JsonSerializer.Deserialize<List<PrerequisiteNode>>(ref reader, options);
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }


            return new PrerequisiteAndGroup(
                prerequisites: prerequisites ?? throw new JsonException($"No prerequisites found for {nameof(PrerequisiteAndGroup)}"),
                sourceType: sourceType ?? throw new JsonException($"No sourceType found for {nameof(PrerequisiteAndGroup)}"),
                isActive: isActive ?? throw new JsonException($"No isActive found for {nameof(PrerequisiteAndGroup)}")
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteAndGroup value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(PrerequisiteAndGroup.SourceType), (int)value.SourceType);
            writer.WriteBoolean(nameof(PrerequisiteAndGroup.IsActive), value.IsActive);

            writer.WritePropertyName(nameof(PrerequisiteAndGroup.Prerequisites));
            writer.WriteStartArray();
            foreach (var group in value.Prerequisites)
                JsonSerializer.Serialize(writer, group, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
