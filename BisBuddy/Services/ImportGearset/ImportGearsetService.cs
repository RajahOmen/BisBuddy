using BisBuddy.Gear.GearsetsManager;
using BisBuddy.Import;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BisBuddy.Services.ImportGearset
{
    public class ImportGearsetService : IImportGearsetService
    {
        private readonly Dictionary<ImportGearsetSourceType, IImportGearsetSource> sources;
        private readonly IGearsetsService gearsetsService;
        private readonly IPluginLog pluginLog;

        public ImportGearsetService(
            IEnumerable<IImportGearsetSource> sources,
            IGearsetsService gearsetsService,
            IPluginLog pluginLog
            )
        {
            this.sources = [];
            foreach (var source in sources)
                this.sources[source.SourceType] = source;

            this.gearsetsService = gearsetsService;
            this.pluginLog = pluginLog;
        }

        public IReadOnlyList<ImportGearsetSourceType> RegisteredSourceTypes => [.. sources.Keys];

        public async Task<ImportGearsetsResult> ImportGearsets(ImportGearsetSourceType sourceType, string sourceString)
        {
            try
            {
                if (sourceString.Length == 0)
                    throw new GearsetImportException(GearsetImportStatusType.InvalidInput);

                var loggedSourceString = sourceString[..Math.Min(sourceString.Length, 100)].Replace("\n", "");
                pluginLog.Debug($"Attempting to import {sourceType} gearset from \"{loggedSourceString}\"");

                if (!sources.TryGetValue(sourceType, out var source))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                // import from the gearset source
                var gearsets = await source.ImportGearsets(sourceString);

                if (gearsets.Count == 0)
                    throw new GearsetImportException(GearsetImportStatusType.NoGearsets);

                if (gearsets.Count > Plugin.MaxGearsetCount - gearsetsService.Gearsets.Count)
                    throw new GearsetImportException(GearsetImportStatusType.TooManyGearsets);

                gearsetsService.AddGearsets(gearsets);

                pluginLog.Information($"Successfully added \"{gearsets.Count}\" gearsets from \"{source.SourceType}\"");

                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.Success, Gearsets = gearsets };
            }
            catch (GearsetImportException ex)
            {
                // encountered an expected error case
                pluginLog.Warning(ex, $"{ex.FailStatusType} error encountered when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = ex.FailStatusType, Gearsets = null };
            }
            catch (Exception ex)
            {
                // encountered unexpected error case
                pluginLog.Error(ex, $"Gearset import internal encountered error when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.InternalError, Gearsets = null };
            }
        }
    }

    public interface IImportGearsetService
    {
        public IReadOnlyList<ImportGearsetSourceType> RegisteredSourceTypes { get; }
        public Task<ImportGearsetsResult> ImportGearsets(ImportGearsetSourceType sourceType, string sourceString);
    }
}
