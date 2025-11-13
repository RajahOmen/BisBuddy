using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    public class GearsetsListConverter : JsonConverter<List<Gearset>>
    {
        public override List<Gearset>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            List<Gearset> gearsetList = [];

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected list of gearsets for GearsetListConverter, got {reader.TokenType}");

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (JsonSerializer.Deserialize<Gearset>(ref reader, options) is not Gearset gearset)
                    throw new JsonException($"Expected Gearset object in GearsetListConverter, got {reader.TokenType}");

                gearsetList.Add(gearset);
            }

            updateNullPriorities(gearsetList);

            return gearsetList;
        }

        private void updateNullPriorities(List<Gearset> gearsets)
        {
            var nullPriorities = gearsets
                .Where(g => g.Priority is null)
                .Count();
            foreach (var gearset in gearsets)
                gearset.Priority ??= nullPriorities--;
        }

        public override void Write(Utf8JsonWriter writer, List<Gearset> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var gearset in value)
                JsonSerializer.Serialize(writer, gearset, options);

            writer.WriteEndArray();
        }
    }
}
