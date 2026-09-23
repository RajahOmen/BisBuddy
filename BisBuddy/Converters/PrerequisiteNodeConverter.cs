using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteNodeConverter(IItemDataService itemData) : JsonConverter<PrerequisiteNode>
    {
        public const string TypeDescriminatorPropertyName = "$type";
        private const string DisabledPrereqsPropName = "DisabledPrereqs";
        private readonly IItemDataService itemData = itemData;

        public override PrerequisiteNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected StartObject for {nameof(PrerequisiteNode)}");

            uint? itemId = null;
            string? itemName = null;
            string? nodeId = null;
            List<PrerequisiteNode>? prerequisiteTree = null;
            List<int>? disabledPrereqIdxs = null;
            bool? isCollected = null;
            bool? collectLock = null;
            PrerequisiteNodeSourceType? sourceType = null;
            ChildGroupType? groupType = null;

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
                    case nameof(PrerequisiteNode.SourceType):
                        sourceType = (PrerequisiteNodeSourceType)reader.GetInt32();
                        break;
                    case nameof(PrerequisiteNode.GroupType):
                        groupType = (ChildGroupType)reader.GetInt32();
                        break;
                    case nameof(PrerequisiteNode.PrerequisiteTree):
                        prerequisiteTree = JsonSerializer.Deserialize<List<PrerequisiteNode>>(ref reader, options);
                        break;
                    case DisabledPrereqsPropName:
                        disabledPrereqIdxs = JsonSerializer.Deserialize<List<int>>(ref reader, options);
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }

            groupType ??= (prerequisiteTree?.Count ?? 0) == 0 ? ChildGroupType.Unit : null;

            return new PrerequisiteNode(
                itemId: itemId ?? throw new JsonException($"No itemId found for {nameof(PrerequisiteNode)}"),
                itemName: itemName ?? throw new JsonException($"No itemName found for {nameof(PrerequisiteNode)}"),
                prerequisiteTree: prerequisiteTree,
                isCollected: isCollected ?? false,
                collectLock: collectLock ?? false,
                sourceType: sourceType ?? throw new JsonException($"No sourceType found for {nameof(PrerequisiteNode)}"),
                groupType: groupType ?? throw new JsonException($"No groupType found for {nameof(PrerequisiteNode)}"),
                nodeId: nodeId ?? throw new JsonException($"No nodeId found for {nameof(PrerequisiteNode)}"),
                disabledPrereqs: disabledPrereqIdxs
                );
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(PrerequisiteNode.NodeId), value.NodeId);
            writer.WriteNumber(nameof(PrerequisiteNode.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(PrerequisiteNode.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(PrerequisiteNode.CollectLock), value.CollectLock);
            writer.WriteNumber(nameof(PrerequisiteNode.SourceType), (int)value.SourceType);
            writer.WriteNumber(nameof(PrerequisiteNode.GroupType), (int)value.GroupType);

            writer.WritePropertyName(nameof(PrerequisiteNode.PrerequisiteTree));
            writer.WriteStartArray();
            foreach (var (node, isActive) in value.CompletePrerequisiteTree)
                JsonSerializer.Serialize(writer, node, options);
            writer.WriteEndArray();

            var disabledPrereqs = value.CompletePrerequisiteTree
                .Index()
                .Where(indexedEntry => !indexedEntry.Item.IsActive)
                .Select(indexedEntry => indexedEntry.Index);

            if (disabledPrereqs.Any())
            {
                writer.WritePropertyName(DisabledPrereqsPropName);
                writer.WriteStartArray();
                foreach (var idx in disabledPrereqs)
                    JsonSerializer.Serialize(writer, idx, options);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}
