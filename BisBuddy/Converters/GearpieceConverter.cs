using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class GearpieceConverter(
        IGearpieceFactory gearpieceFactory
        ) : JsonConverter<Gearpiece>
    {
        private readonly IGearpieceFactory gearpieceFactory = gearpieceFactory;

        public override Gearpiece? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Gearpiece");

            uint? itemId = null;
            IPrerequisiteNode? prerequisiteTree = null;
            List<Materia>? itemMateria = null;
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
                    case nameof(Gearpiece.ItemId):
                        itemId = reader.GetUInt32();
                        break;
                    case nameof(Gearpiece.PrerequisiteTree):
                        prerequisiteTree = JsonSerializer.Deserialize<IPrerequisiteNode>(ref reader, options);
                        break;
                    case nameof(Gearpiece.ItemMateria):
                        itemMateria = JsonSerializer.Deserialize<List<Materia>>(ref reader, options);
                        break;
                    case nameof(Gearpiece.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(Gearpiece.IsManuallyCollected):
                        isManuallyCollected = reader.GetBoolean();
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }

            if (itemId == null)
                throw new JsonException("No itemId found for Gearpiece");

            return gearpieceFactory.Create(
                itemId!.Value,
                itemMateria,
                prerequisiteTree,
                isCollected ?? false,
                isManuallyCollected ?? false
                );
        }

        public override void Write(Utf8JsonWriter writer, Gearpiece value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(Gearpiece.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(Gearpiece.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(Gearpiece.IsManuallyCollected), value.IsManuallyCollected);
            writer.WriteNumber(nameof(Gearpiece.GearpieceType), (int)value.GearpieceType);

            writer.WritePropertyName(nameof(Gearpiece.ItemMateria));
            JsonSerializer.Serialize(writer, value.ItemMateria, options);

            writer.WritePropertyName(nameof(Gearpiece.PrerequisiteTree));
            JsonSerializer.Serialize(writer, value.PrerequisiteTree, options);

            writer.WriteEndObject();
        }
    }
}
