using BisBuddy.Gear;
using BisBuddy.Items;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class MateriaConverter(IItemDataService itemData) : JsonConverter<Materia>
    {
        private readonly IItemDataService itemData = itemData;

        public override Materia? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Materia");

            uint? itemId = null;
            bool? isMelded = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(Materia.ItemId):
                        itemId = reader.GetUInt32();
                        break;
                    case nameof(Materia.IsMelded):
                        isMelded = reader.GetBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return itemData.BuildMateria(
                itemId ?? throw new JsonException("No itemId found for Materia"),
                isMelded ?? throw new JsonException("No isMelded found for Materia")
                );
        }

        public override void Write(Utf8JsonWriter writer, Materia value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(Materia.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(Materia.IsMelded), value.IsMelded);

            writer.WriteEndObject();
        }
    }
}
