using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class ImportGearsetService(Plugin plugin)
    {
        private readonly Dictionary<ImportSourceType, ImportSource> sources = [];
        private readonly Plugin plugin = plugin;

        public ImportGearsetService RegisterSource(ImportSourceType sourceType, ImportSource source)
        {
            sources[sourceType] = source;
            return this;
        }

        public List<ImportSourceType> RegisteredSources()
        {
            return [.. sources.Keys];
        }

        public async Task<ImportGearsetsResult> ImportGearsets(ImportSourceType sourceType, string sourceString)
        {
            try
            {
                if (sourceString.Length == 0)
                    throw new GearsetImportException(GearsetImportStatusType.InvalidInput);

                Services.Log.Debug($"Attempting to import {sourceType} gearset from \"{sourceString[.. Math.Min(sourceString.Length, 100)]}\"");

                // don't have a source registered for this type
                if (!sources.TryGetValue(sourceType, out var source))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                // import from the gearset source
                var gearsets = await source.ImportGearsets(sourceString);

                if (gearsets.Count == 0)
                    throw new GearsetImportException(GearsetImportStatusType.NoGearsets);

                if (gearsets.Count > (Plugin.MaxGearsetCount - plugin.Gearsets.Count))
                    throw new GearsetImportException(GearsetImportStatusType.TooManyGearsets);

                plugin.Gearsets.AddRange(gearsets);
                plugin.SaveGearsetsWithUpdate(true);
                Services.Log.Information($"Successfully added \"{gearsets.Count}\" gearsets from \"{source.SourceType}\"");

                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.Success, Gearsets = gearsets };
            }
            catch (GearsetImportException ex)
            {
                // encountered an expected error case
                Services.Log.Warning(ex, $"{ex.FailStatusType} error encountered when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = ex.FailStatusType, Gearsets = null };
            }
            catch (Exception ex)
            {
                // encountered unexpected error case
                Services.Log.Error(ex, $"Gearset import internal encountered error when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.InternalError, Gearsets = null };
            }
        }
    }
}
