using System;
using System.Collections.Generic;

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
        public GearsetSourceType? SourceType { get; set; }
        public string JobAbbrv { get; set; } = "???";

        public Gearset(string name, string? sourceUrl, GearsetSourceType? sourceType)
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceType = sourceType;
        }

        protected Gearset(
            string name,
            List<Gearpiece> gearpieces,
            string jobAbbrv,
            string? sourceUrl,
            GearsetSourceType? sourceType
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

        public List<(Gearpiece gearpiece, int countNeeded)> GetGearpiecesNeedingItem(uint id, bool includeCollectedPrereqs)
        {
            List<(Gearpiece gearpiece, int countNeeded)> satisfiedGearpieces = [];
            foreach (var gearpiece in Gearpieces)
            {
                var countNeeded = gearpiece.NeedsItemId(id, includeCollectedPrereqs);
                if (countNeeded > 0)
                {
                    satisfiedGearpieces.Add((gearpiece, countNeeded));
                }
            }
            return satisfiedGearpieces;
        }
    }
}
