using BisBuddy.Import;
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
        // for links to externally sourced sites
        public string? SourceUrl { get; set; } = null;
        // for local representations of the gearset that aren't native JSON (ex: teamcraft plaintext)
        public string? SourceString { get; set; } = null;
        public ImportSourceType? SourceType { get; set; }
        public string JobAbbrv { get; set; } = "???";
        public HighlightColor? HighlightColor { get; set; } = null;

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
            ImportSourceType? sourceType,
            string? sourceUrl = null,
            string? sourceString = null
            )
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceString = sourceString;
            SourceType = sourceType;
            Gearpieces = gearpieces;
            JobAbbrv = jobAbbrv;
            // ensure ordering after adding
            Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));
        }

        public IEnumerable<ItemRequirement> ItemRequirements(bool includeUncollectedItemMateria)
        {
            if (!IsActive)
                yield break;

            foreach (var gearpiece in Gearpieces)
                foreach (var requirement in gearpiece.ItemRequirements(this, includeUncollectedItemMateria))
                    yield return requirement;
        }
    }
}
