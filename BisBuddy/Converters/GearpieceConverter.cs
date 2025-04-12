using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class GearpieceConverter(ItemData itemData) : JsonConverter<Gearpiece>
    {
        private readonly ItemData itemData = itemData;

        public override Gearpiece? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Gearpiece");

            uint? itemId = null;
            PrerequisiteNode? prerequisiteTree = null;
            List<Materia>? itemMateria = null;
            bool? isCollected = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(Gearpiece.ItemId):
                        itemId = reader.GetUInt32();
                        break;
                    case nameof(Gearpiece.PrerequisiteTree):
                        prerequisiteTree = JsonSerializer.Deserialize<PrerequisiteNode>(ref reader, options);
                        break;
                    case nameof(Gearpiece.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(Gearpiece.ItemMateria):
                        itemMateria = JsonSerializer.Deserialize<List<Materia>>(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return itemData.BuildGearpiece(
                itemId ?? throw new JsonException("No itemId found for Gearpiece"),
                prerequisiteTree,
                itemMateria ?? throw new JsonException("No itemMateria found for Gearpiece"),
                isCollected ?? throw new JsonException("No isCollected found for Gearpiece")
                );
        }

        public override void Write(Utf8JsonWriter writer, Gearpiece value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(Gearpiece.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(Gearpiece.IsCollected), value.IsCollected);
            writer.WriteNumber(nameof(Gearpiece.GearpieceType), (int)value.GearpieceType);

            writer.WritePropertyName(nameof(Gearpiece.ItemMateria));
            JsonSerializer.Serialize(writer, value.ItemMateria, options);

            writer.WritePropertyName(nameof(Gearpiece.PrerequisiteTree));
            JsonSerializer.Serialize(writer, value.PrerequisiteTree, options);

            writer.WriteEndObject();
        }
    }
}
