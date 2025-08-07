using BisBuddy.Gear.Melds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    public class MateriaGroupConverter : JsonConverter<MateriaGroup>
    {
        public override MateriaGroup? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var materiaList = JsonSerializer.Deserialize<List<Materia>>(ref reader, options);
            return new MateriaGroup(materiaList);
        }

        public override void Write(Utf8JsonWriter writer, MateriaGroup value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.ToList(), options);
        }
    }
}
