using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class GearpieceConverter(
        IGearpieceFactory gearpieceFactory
        ) : JsonConverter<Gearpiece>
    {
        private const string LegacyIsManuallyCollectedPropertyName = "IsManuallyCollected";
        private readonly IGearpieceFactory gearpieceFactory = gearpieceFactory;

        public override Gearpiece? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Gearpiece");

            uint? itemId = null;
            IPrerequisiteNode? prerequisiteTree = null;
            MateriaGroup? itemMateria = null;
            bool? isCollected = null;
            bool? isManuallyCollected = null;
            bool? collectionStatusLocked = null;

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
                        itemMateria = JsonSerializer.Deserialize<MateriaGroup>(ref reader, options);
                        break;
                    case nameof(Gearpiece.IsCollected):
                        isCollected = reader.GetBoolean();
                        break;
                    case nameof(Gearpiece.CollectLock):
                        isManuallyCollected = reader.GetBoolean();
                        break;
                    case LegacyIsManuallyCollectedPropertyName:
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
                collectionStatusLocked ?? isManuallyCollected ?? false
                );
        }

        public override void Write(Utf8JsonWriter writer, Gearpiece value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber(nameof(Gearpiece.ItemId), value.ItemId);
            writer.WriteBoolean(nameof(Gearpiece.IsCollected), value.IsCollected);
            writer.WriteBoolean(nameof(Gearpiece.CollectLock), value.CollectLock);

            writer.WritePropertyName(nameof(Gearpiece.ItemMateria));
            JsonSerializer.Serialize(writer, value.ItemMateria, options);

            writer.WritePropertyName(nameof(Gearpiece.PrerequisiteTree));
            JsonSerializer.Serialize(writer, value.PrerequisiteTree, options);

            writer.WriteEndObject();
        }
    }
}
