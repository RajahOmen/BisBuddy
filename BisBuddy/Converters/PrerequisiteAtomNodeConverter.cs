using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BisBuddy.Converters
{
    internal class PrerequisiteAtomNodeConverter(ItemData itemData) : JsonConverter<PrerequisiteAtomNode>
    {
        private readonly ItemData itemData = itemData;

        public override PrerequisiteAtomNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for PrerequisiteAtomNode");

            uint? itemId = null;
            string? itemName = null;
            string? nodeId = null;
            List<PrerequisiteNode>? prerequisiteTree = null;
            PrerequisiteNodeSourceType? sourceType = null;
            bool? isCollected = null;
            bool? isManuallyCollected = null;

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
                    case nameof(PrerequisiteNode.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(PrerequisiteNode.IsManuallyCollected):
                        isManuallyCollected = reader.GetBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new PrerequisiteAtomNode(
                itemId ?? throw new JsonException("No itemId found for PrerequisiteAtomNode"),
                itemName ?? throw new JsonException("No itemName found for PrerequisiteAtomNode"),
                prerequisiteTree,
                sourceType ?? throw new JsonException("No sourceType found for PrerequisiteAtomNode"),
                isCollected ?? throw new JsonException("No isCollected found for PrerequisiteAtomNode"),
                isManuallyCollected ?? throw new JsonException("No isManuallyCollected found for PrerequisiteAtomNode"),
                nodeId ?? throw new JsonException("No nodeId found for PrerequisiteAtomNode")
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteAtomNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(PrerequisiteNode.NodeId), value.NodeId);
            writer.WriteNumber(nameof(PrerequisiteNode.ItemId), value.ItemId);
            writer.WriteNumber(nameof(PrerequisiteNode.SourceType), (int)value.SourceType);
            writer.WriteBoolean(nameof(PrerequisiteNode.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(PrerequisiteNode.IsManuallyCollected), value.IsManuallyCollected);

            writer.WritePropertyName(nameof(PrerequisiteNode.PrerequisiteTree));
            writer.WriteStartArray();
            foreach (var prerequisiteNode in value.PrerequisiteTree)
                JsonSerializer.Serialize(writer, prerequisiteNode, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
