using BisBuddy.Gear;
using System.Collections.Generic;

namespace BisBuddy.Import
{
    public class ImportService
    {
        private readonly List<GearsetSource> sources = [];

        public void RegisterSource(GearsetSource source)
        {
            sources.Add(source);
        }

        public List<Gearset> Import(string sourceString, GearsetSourceType? type)
        {
            if (sourceString.Length == 0)
            {
                var noInputFailType = type == GearsetSourceType.Remote
                    ? GearsetImportStatusType.InvalidUrl
                    : GearsetImportStatusType.InvalidStringInput;
                throw new GearsetImportException(noInputFailType);
            }

            foreach (var source in sources)
            {
                if (type != null && source.SourceType != type)
                    continue;

                if (source.IsSource(sourceString))
                {
                    return source.Import(sourceString);
                }
            }

            var noMatchFailType = type == GearsetSourceType.Remote
                ? GearsetImportStatusType.InvalidUrl
                : GearsetImportStatusType.InvalidStringInput;
            throw new GearsetImportException(noMatchFailType);
        }
    }
}
