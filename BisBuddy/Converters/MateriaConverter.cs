using BisBuddy.Factories;
using BisBuddy.Gear.Melds;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class MateriaConverter(
        IMateriaFactory materiaFactory
        ) : JsonConverter<Materia>
    {
        private const string LegacyIsMeldedPropertyName = "IsMelded";
        private readonly IMateriaFactory materiaFactory = materiaFactory;

        public override Materia? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Materia");

            uint? itemId = null;
            bool? isCollected = null;
            bool? isMelded = null;
            bool collectLock = false;

            var items = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                items.Add(propertyName ?? "<null>");
                switch (propertyName)
                {
                    case nameof(Materia.ItemId):
                        itemId = reader.GetUInt32();
                        break;
                    case nameof(Materia.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(Materia.CollectLock):
                        collectLock = reader.GetBoolean();
                        break;
                    case LegacyIsMeldedPropertyName:
                        isMelded = reader.GetBoolean();
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }

            return materiaFactory.Create(
                itemId ?? throw new JsonException("No itemId found for Materia"),
                isCollected ?? isMelded ?? throw new JsonException($"No isCollected/isMelded found for Materia"),
                collectLock
                );
        }

        public override void Write(Utf8JsonWriter writer, Materia value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(Materia.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(Materia.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(Materia.CollectLock), value.CollectLock);

            writer.WriteEndObject();
        }
    }
}
