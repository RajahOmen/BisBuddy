using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class JsonSource : ImportSource
    {
        public ImportSourceType SourceType => ImportSourceType.Json;

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            try
            {
                var task = new Task<List<Gearset>>(() =>
                {
                    var gearset = JsonSerializer.Deserialize<Gearset>(importString, Configuration.JsonOptions)
                        ?? throw new GearsetImportException(GearsetImportStatusType.InvalidInput);

                    gearset.Id = Guid.NewGuid().ToString(); // set to a new random uuid
                    return [gearset];
                });

                task.RunSynchronously(TaskScheduler.Default);
                return await task;
            }
            catch (ArgumentNullException ex)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, ex.Message);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, ex.Message);
            }
        }
    }
}
