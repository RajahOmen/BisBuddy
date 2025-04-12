using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class JsonSource(JsonSerializerOptions jsonOptions) : ImportSource
    {
        public ImportSourceType SourceType => ImportSourceType.Json;
        private JsonSerializerOptions jsonOptions = jsonOptions;

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            try
            {
                var gearset = await Task.Run(() => parseGearset(importString))
                    ?? throw new GearsetImportException(GearsetImportStatusType.InvalidInput);

                return [gearset];
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is ArgumentNullException)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, ex.Message);
            }
        }

        private Gearset? parseGearset(string importString)
        {
            var gearset = JsonSerializer.Deserialize<Gearset>(importString, jsonOptions);
            if (gearset == null)
                return null;

            gearset.Id = Guid.NewGuid().ToString(); // set to a new random uuid
            return gearset;
        }
    }
}
