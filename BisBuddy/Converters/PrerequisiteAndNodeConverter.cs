using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteAndNodeConverter(IItemDataService itemData) : JsonConverter<PrerequisiteAndNode>
    {
        public const string TypeDescriminatorValue = "and";
        private readonly IItemDataService itemData = itemData;

        public override PrerequisiteAndNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for PrerequisiteAndNode");

            uint? itemId = null;
            string? itemName = null;
            string? nodeId = null;
            List<IPrerequisiteNode>? prerequisiteTree = null;
            PrerequisiteNodeSourceType? sourceType = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(IPrerequisiteNode.ItemId):
                        itemId = reader.GetUInt32();
                        itemName = itemData.GetItemNameById(reader.GetUInt32());
                        break;
                    case nameof(IPrerequisiteNode.NodeId):
                        nodeId = reader.GetString();
                        break;
                    case nameof(IPrerequisiteNode.SourceType):
                        sourceType = (PrerequisiteNodeSourceType)reader.GetInt32();
                        break;
                    case nameof(IPrerequisiteNode.PrerequisiteTree):
                        prerequisiteTree = JsonSerializer.Deserialize<List<IPrerequisiteNode>>(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new PrerequisiteAndNode(
                itemId ?? throw new JsonException("No itemId found for PrerequisiteAndNode"),
                itemName ?? throw new JsonException("No itemName found for PrerequisiteAndNode"),
                prerequisiteTree,
                sourceType ?? throw new JsonException("No sourceType found for PrerequisiteAndNode"),
                nodeId ?? throw new JsonException("No nodeId found for PrerequisiteAndNode")
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteAndNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(PrerequisiteNodeConverter.TypeDescriminatorPropertyName, TypeDescriminatorValue);
            writer.WriteString(nameof(IPrerequisiteNode.NodeId), value.NodeId);
            writer.WriteNumber(nameof(IPrerequisiteNode.ItemId), value.ItemId);
            writer.WriteNumber(nameof(IPrerequisiteNode.SourceType), (int)value.SourceType);

            writer.WritePropertyName(nameof(IPrerequisiteNode.PrerequisiteTree));
            writer.WriteStartArray();
            foreach (var prerequisiteNode in value.PrerequisiteTree)
                JsonSerializer.Serialize(writer, prerequisiteNode, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
