using BisBuddy.Gear;
using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Services.ImportGearset
{
    public class JsonSource(JsonSerializerOptions jsonOptions) : IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType => ImportGearsetSourceType.Json;
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
