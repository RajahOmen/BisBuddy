using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BisBuddy.Services.ImportGearset
{
    public class ImportGearsetService : IImportGearsetService
    {
        private readonly ITypedLogger<ImportGearsetService> logger;
        private readonly Dictionary<ImportGearsetSourceType, IImportGearsetSource> sources;

        public ImportGearsetService(
            ITypedLogger<ImportGearsetService> logger,
            IEnumerable<IImportGearsetSource> sources
            )
        {
            this.sources = [];
            foreach (var source in sources)
                this.sources[source.SourceType] = source;

            this.logger = logger;
        }

        public IReadOnlyList<ImportGearsetSourceType> RegisteredSourceTypes => [.. sources.Keys];

        public async Task<ImportGearsetsResult> ImportGearsets(
            ImportGearsetSourceType sourceType,
            string sourceString,
            int gearsetCapacity
            )
        {
            try
            {
                if (sourceString.Length == 0)
                    throw new GearsetImportException(GearsetImportStatusType.InvalidInput);

                var loggedSourceString = sourceString[..Math.Min(sourceString.Length, 100)].Replace("\n", "");
                logger.Debug($"Attempting to import {sourceType} gearset from \"{loggedSourceString}\"");

                if (!sources.TryGetValue(sourceType, out var source))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                // import from the gearset source
                var gearsets = await source.ImportGearsets(sourceString);

                if (gearsets.Count == 0)
                    throw new GearsetImportException(GearsetImportStatusType.NoGearsets);

                if (gearsets.Count > gearsetCapacity)
                    throw new GearsetImportException(GearsetImportStatusType.TooManyGearsets);

                logger.Info($"Successfully imported \"{gearsets.Count}\" gearsets from \"{source.SourceType}\"");

                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.Success, Gearsets = gearsets };
            }
            catch (GearsetImportException ex)
            {
                // encountered an expected error case
                logger.Warning(ex, $"{ex.FailStatusType} error encountered when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = ex.FailStatusType, Gearsets = null };
            }
            catch (Exception ex)
            {
                // encountered unexpected error case
                logger.Error(ex, $"Gearset import internal encountered error when importing from \"{sourceString}\"");
                return new ImportGearsetsResult { StatusType = GearsetImportStatusType.InternalError, Gearsets = null };
            }
        }
    }

    public interface IImportGearsetService
    {
        public Task<ImportGearsetsResult> ImportGearsets(
            ImportGearsetSourceType sourceType,
            string sourceString,
            int gearsetCapacity
            );
    }
}
