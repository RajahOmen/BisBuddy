using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteAtomNodeConverter(ItemData itemData) : JsonConverter<PrerequisiteAtomNode>
    {
        public const string TypeDescriminatorValue = "atom";
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
            bool isMeldable = false;

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
                        itemName = itemData.GetItemNameById(itemId!.Value);
                        isMeldable = itemData.ItemIsMeldable(itemId!.Value);
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

            if (itemId == null)
                throw new JsonException("No itemId found for PrerequisiteAtomNode");

            // try to extend this tree with new options
            var prerequisiteNode = prerequisiteTree != null
                ? prerequisiteTree.Count > 0
                ? prerequisiteTree[0]
                : null
                : null;

            var newPrerequisiteNode = itemData.ExtendItemPrerequisites(
                itemId!.Value,
                prerequisiteNode,
                isCollected ?? false,
                isManuallyCollected ?? false
                );

            if (newPrerequisiteNode != null)
                prerequisiteTree = [newPrerequisiteNode];

            return new PrerequisiteAtomNode(
                itemId!.Value,
                itemName ?? throw new JsonException("No itemName found for PrerequisiteAtomNode"),
                prerequisiteTree,
                sourceType ?? throw new JsonException("No sourceType found for PrerequisiteAtomNode"),
                isCollected ?? throw new JsonException("No isCollected found for PrerequisiteAtomNode"),
                isManuallyCollected ?? throw new JsonException("No isManuallyCollected found for PrerequisiteAtomNode"),
                nodeId ?? throw new JsonException("No nodeId found for PrerequisiteAtomNode"),
                isMeldable
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteAtomNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(PrerequisiteNodeConverter.TypeDescriminatorPropertyName, TypeDescriminatorValue);
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
