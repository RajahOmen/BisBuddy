using BisBuddy.Gear.Prerequisites;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    internal class PrerequisiteNodeConverter : JsonConverter<PrerequisiteNode>
    {
        public const string TypeDescriminatorPropertyName = "$type";

        public override PrerequisiteNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for PrerequisiteNode");

            using var document = JsonDocument.ParseValue(ref reader);
            var rootNode = document.RootElement;

            if (!rootNode.TryGetProperty(TypeDescriminatorPropertyName, out var typeDescriminator))
                throw new JsonException("PrerequisiteNode with no derived type parameter found");

            if (typeDescriminator.ValueKind != JsonValueKind.String)
                throw new JsonException($"PrerequisiteNode with invalid derived type parameter \"{typeDescriminator}\" found");

            return typeDescriminator.GetString() switch
            {
                PrerequisiteAndNodeConverter.TypeDescriminatorValue => JsonSerializer.Deserialize<PrerequisiteAndNode>(document, options),
                PrerequisiteAtomNodeConverter.TypeDescriminatorValue => JsonSerializer.Deserialize<PrerequisiteAtomNode>(document, options),
                PrerequisiteOrNodeConverter.TypeDescriminatorValue => JsonSerializer.Deserialize<PrerequisiteOrNode>(document, options),
                _ => throw new JsonException($"PrerequisiteNode with invalid derived type parameter \"{typeDescriminator}\" found")
            };
        }

        public override void Write(Utf8JsonWriter writer, PrerequisiteNode value, JsonSerializerOptions options)
        {
            if (value is PrerequisiteAndNode andNode)
                JsonSerializer.Serialize(writer, andNode, options);
            else if (value is PrerequisiteAtomNode atomNode)
                JsonSerializer.Serialize(writer, atomNode, options);
            else if (value is PrerequisiteOrNode orNode)
                JsonSerializer.Serialize(writer, orNode, options);
            else
                throw new JsonException($"Prerequisite node of unknown type: \"{value.GetType()}\"");
        }
    }
}
