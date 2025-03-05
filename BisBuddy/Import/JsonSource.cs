using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class JsonSource : ImportSource
    {
        public ImportSourceType SourceType => throw new NotImplementedException();

        public Task<List<Gearset>> ImportGearsets(string importString)
        {
            throw new NotImplementedException();
        }


        public static Gearset ImportFromJson(string jsonStr)
        {
            try
            {
                var gearset = JsonSerializer.Deserialize<Gearset>(jsonStr, Configuration.JsonOptions)
                    ?? throw new GearsetImportException(GearsetImportStatusType.InternalError);

                gearset.Id = Guid.NewGuid().ToString(); // set to a new random uuid\
                Services.Log.Debug($"Imported 1 gearset from {jsonStr}");
                return gearset;
            }
            catch (ArgumentNullException)
            {
                Services.Log.Error($"Gearset Import Null JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                Services.Log.Error(ex, $"Gearset Import Invalid JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Gearset Import Internal Error for JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
        }
    }
}
