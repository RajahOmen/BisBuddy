using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy.Converters
{
    public class GearsetConverter(
        IGearsetFactory gearsetFactory,
        IItemDataService itemDataService
        ) : JsonConverter<Gearset>
    {
        private const string ClassJobIdPropertyName = "ClassJobId";
        private const string JobAbbrevPropertyName = "JobAbbrv";

        public override Gearset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected StartObject for {nameof(Gearset)}");

            // gearset properties to deserialize
            string? id = null;
            string? name = null;
            List<Gearpiece>? gearpieces = null;
            ImportGearsetSourceType? sourceType = null;
            uint? classJobId = null;
            bool? isActive = null;
            string? sourceUrl = null;
            string? sourceString = null;
            int? priority = null;
            DateTime? importDate = null;
            HighlightColor? highlightColor = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(Gearset.Id):
                        id = reader.GetString();
                        break;
                    case nameof(Gearset.Name):
                        name = reader.GetString();
                        break;
                    case nameof(Gearset.Gearpieces):
                        gearpieces = JsonSerializer.Deserialize<List<Gearpiece>>(ref reader, options);
                        break;
                    case nameof(Gearset.SourceType):
                        sourceType = (ImportGearsetSourceType)reader.GetInt32();
                        break;
                    case ClassJobIdPropertyName:
                        classJobId = reader.GetUInt32();
                        break;
                    // legacy encoding via string abbreviation
                    case JobAbbrevPropertyName:
                        classJobId = itemDataService
                            .GetClassJobInfoByEnAbbreviation(reader.GetString() ?? "")
                            .ClassJobId;
                        break;
                    case nameof(Gearset.IsActive):
                        isActive = reader.GetBoolean();
                        break;
                    case nameof(Gearset.SourceUrl):
                        sourceUrl = reader.GetString();
                        break;
                    case nameof(Gearset.SourceString):
                        sourceString = reader.GetString();
                        break;
                    case nameof(Gearset.Priority):
                        priority = reader.GetInt32();
                        break;
                    case nameof(Gearset.ImportDate):
                        importDate = reader.GetDateTime();
                        break;
                    case nameof(Gearset.HighlightColor):
                        highlightColor = JsonSerializer.Deserialize<HighlightColor>(ref reader, options);
                        break;
                    default:
                        reader.TrySkip();
                        break;
                }
            }

            // try to get the class job id if none was found
            if ((classJobId is null || classJobId == 0) && gearpieces is not null)
            {
                var validJobIds = itemDataService
                    .FindClassJobIdUsers(gearpieces.Select(g => g.ItemId));
                if (validJobIds.Count() == 1)
                    classJobId = validJobIds.First();
            }

            return gearsetFactory.Create(
                id: id ?? throw new JsonException($"Gearset with no id found"),
                name: name ?? throw new JsonException($"No gearset {nameof(Gearset.Name)} found for {id}"),
                gearpieces: gearpieces ?? throw new JsonException($"No gearset {nameof(Gearset.Gearpieces)} found for {id}"),
                sourceType: sourceType,
                classJobId: classJobId ?? 0,
                isActive: isActive ?? throw new JsonException($"No gearset {nameof(Gearset.IsActive)} found for {id}"),
                sourceUrl: sourceUrl,
                sourceString: sourceString,
                priority: priority,
                importDate: importDate,
                highlightColor: highlightColor
                );
        }

        public override void Write(Utf8JsonWriter writer, Gearset value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(Gearset.Id), value.Id);
            writer.WriteString(nameof(Gearset.Name), value.Name);

            writer.WritePropertyName(nameof(Gearset.SourceType));
            JsonSerializer.Serialize(writer, value.SourceType, options);

            writer.WriteNumber(ClassJobIdPropertyName, value.ClassJobInfo.ClassJobId);
            writer.WriteBoolean(nameof(Gearset.IsActive), value.IsActive);
            writer.WriteString(nameof(Gearset.SourceUrl), value.SourceUrl);
            writer.WriteString(nameof(Gearset.SourceString), value.SourceString);

            writer.WritePropertyName(nameof(Gearset.Priority));
            JsonSerializer.Serialize(writer, value.Priority, options);

            writer.WritePropertyName(nameof(Gearset.ImportDate));
            JsonSerializer.Serialize(writer, value.ImportDate, options);

            writer.WritePropertyName(nameof(Gearset.HighlightColor));
            JsonSerializer.Serialize(writer, value.HighlightColor, options);

            writer.WritePropertyName(nameof(Gearset.Gearpieces));
            JsonSerializer.Serialize(writer, value.Gearpieces, options);

            writer.WriteEndObject();
        }
    }
}
