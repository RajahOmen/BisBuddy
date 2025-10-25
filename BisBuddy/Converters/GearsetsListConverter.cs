using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    public class GearsetsListConverter : JsonConverter<List<Gearset>>
    {
        private JsonSerializerOptions? jsonSerializerOptions = null;

        /// <summary>
        /// Create a copy of the provided serializer options, but without this converter in the list.
        /// </summary>
        /// <param name="options">The original JsonSerializerOptions object</param>
        /// <returns>A clone of the options object, without any custom converters for List<Gearset></Gearset></returns>
        private static JsonSerializerOptions createOptions(JsonSerializerOptions options)
        {
            var newJsonSerializerOptions = new JsonSerializerOptions();

            var type = typeof(JsonSerializerOptions);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (prop.Name == nameof(JsonSerializerOptions.Converters))
                    continue;

                var value = prop.GetValue(options);
                prop.SetValue(newJsonSerializerOptions, value);
            }

            foreach (var converter in options.Converters)
            {
                // remove any converter for this from this list
                if (converter.Type != typeof(List<Gearset>))
                    newJsonSerializerOptions.Converters.Add(converter);
            }

            return newJsonSerializerOptions;
        }

        public override List<Gearset>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            jsonSerializerOptions ??= createOptions(options);

            var list = JsonSerializer.Deserialize<List<Gearset>>(ref reader, jsonSerializerOptions);

            if (list is not List<Gearset> gearsetList)
                return list;

            var priority = gearsetList
                .Where(g => g.Priority == null)
                .Count();

            foreach (var gearset in list)
                gearset.Priority ??= priority--;

            return gearsetList;
        }

        public override void Write(Utf8JsonWriter writer, List<Gearset> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, jsonSerializerOptions);
        }
    }
}
