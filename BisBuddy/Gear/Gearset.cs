using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BisBuddy.Gear
{

    [Serializable]
    public partial class Gearset
    {
        public static readonly string DefaultName = "New Gearset";

        // set to random uuid
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool IsActive { get; set; } = true;
        public string Name { get; set; } = "New Gearset";
        public List<Gearpiece> Gearpieces { get; init; } = [];
        public string? SourceUrl { get; set; } = null;
        public ImportSourceType? SourceType { get; set; }
        public string JobAbbrv { get; set; } = "???";

        public Gearset(string name, string? sourceUrl, ImportSourceType? sourceType)
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceType = sourceType;
        }

        internal Gearset(
            string name,
            List<Gearpiece> gearpieces,
            string jobAbbrv,
            string? sourceUrl,
            ImportSourceType? sourceType
            )
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceType = sourceType;
            Gearpieces = gearpieces;
            JobAbbrv = jobAbbrv;
            // ensure ordering after adding
            Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));
        }

        public List<(Gearpiece gearpiece, int countNeeded)> GetGearpiecesNeedingItem(uint candidateItemId, bool ignoreCollected, bool includeCollectedPrereqs)
        {
            List<(Gearpiece gearpiece, int countNeeded)> satisfiedGearpieces = [];
            foreach (var gearpiece in Gearpieces)
            {
                var countNeeded = gearpiece.NeedsItemId(candidateItemId, ignoreCollected, includeCollectedPrereqs);
                if (countNeeded > 0)
                {
                    satisfiedGearpieces.Add((gearpiece, countNeeded));
                }
            }
            return satisfiedGearpieces;
        }

        public string ExportToJsonStr()
        {
            return JsonSerializer.Serialize(this, Configuration.JsonOptions);
        }
    }
}
