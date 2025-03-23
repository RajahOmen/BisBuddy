using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteOrNodeConverter(ItemData itemData) : JsonConverter<PrerequisiteOrNode>
    {
        private readonly ItemData itemData = itemData;

        public override PrerequisiteOrNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for PrerequisiteOrNode");

            uint? itemId = null;
            string? itemName = null;
            string? nodeId = null;
            List<PrerequisiteNode>? prerequisiteTree = null;
            PrerequisiteNodeSourceType? sourceType = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(PrerequisiteNode.ItemId):
                        itemId = reader.GetUInt32();
                        itemName = itemData.GetItemNameById(reader.GetUInt32());
                        break;
                    case nameof(PrerequisiteNode.NodeId):
                        nodeId = reader.GetString();
                        break;
                    case nameof(PrerequisiteNode.SourceType):
                        sourceType = (PrerequisiteNodeSourceType)reader.GetInt32();
                        break;
                    case nameof(PrerequisiteNode.PrerequisiteTree):
                        prerequisiteTree = JsonSerializer.Deserialize<List<PrerequisiteNode>>(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new PrerequisiteOrNode(
                itemId ?? throw new JsonException("No itemId found for PrerequisiteOrNode"),
                itemName ?? throw new JsonException("No itemName found for PrerequisiteOrNode"),
                prerequisiteTree,
                sourceType ?? throw new JsonException("No sourceType found for PrerequisiteOrNode"),
                nodeId ?? throw new JsonException("No nodeId found for PrerequisiteOrNode")
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteOrNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(PrerequisiteNode.NodeId), value.NodeId);
            writer.WriteNumber(nameof(PrerequisiteNode.ItemId), value.ItemId);
            writer.WriteNumber(nameof(PrerequisiteNode.SourceType), (int)value.SourceType);

            writer.WritePropertyName(nameof(PrerequisiteNode.PrerequisiteTree));
            writer.WriteStartArray();
            foreach (var prerequisiteNode in value.PrerequisiteTree)
                JsonSerializer.Serialize(writer, prerequisiteNode, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
