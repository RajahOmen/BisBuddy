using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteNodeConverter(IItemDataService itemData) : JsonConverter<PrerequisiteNode>
    {
        private readonly IItemDataService itemData = itemData;

        public override PrerequisiteNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected StartObject for {nameof(PrerequisiteNode)}");

            uint? itemId = null;
            string? itemName = null;
            string? nodeId = null;
            List<PrerequisiteAndGroup>? completePrerequisiteTree = null;
            bool? isCollected = null;
            bool? collectLock = null;

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
                    case nameof(PrerequisiteNode.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(PrerequisiteNode.CollectLock):
                        collectLock = reader.GetBoolean();
                        break;
                    case nameof(PrerequisiteNode.CompletePrerequisiteTree):
                        completePrerequisiteTree = JsonSerializer.Deserialize<List<PrerequisiteAndGroup>>(ref reader, options);
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }


            return new PrerequisiteNode(
                itemId: itemId ?? throw new JsonException($"No itemId found for {nameof(PrerequisiteNode)}"),
                itemName: itemName ?? throw new JsonException($"No itemName found for {nameof(PrerequisiteNode)}"),
                completePrerequisiteTree: completePrerequisiteTree,
                isCollected: isCollected ?? false,
                collectLock: collectLock ?? false,
                nodeId: nodeId ?? throw new JsonException($"No nodeId found for {nameof(PrerequisiteNode)}")
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(PrerequisiteNode.NodeId), value.NodeId);
            writer.WriteNumber(nameof(PrerequisiteNode.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(PrerequisiteNode.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(PrerequisiteNode.CollectLock), value.CollectLock);

            writer.WritePropertyName(nameof(PrerequisiteNode.CompletePrerequisiteTree));
            writer.WriteStartArray();
            foreach (var group in value.CompletePrerequisiteTree)
                JsonSerializer.Serialize(writer, group, options);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
