using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear
{
    public delegate void GearsetChangeHandler();

    [Serializable]
    public partial class Gearset
    {
        private readonly IReadOnlyList<Gearpiece> gearpieces;

        private string id;
        private bool isActive;
        private string name;
        private int? priority;
        private DateTime importDate;
        private HighlightColor? highlightColor;

        // set to random uuid
        public string Id
        {
            get => id;
            set
            {
                if (id == value)
                    return;

                id = value;
                triggerGearsetChange();
            }
        }
        public bool IsActive {
            get => isActive;
            set
            {
                if (isActive == value)
                    return;

                isActive = value;
                triggerGearsetChange();
            }
        }
        public string Name {
            get => name;
            set
            {
                if (name == value)
                    return;

                name = value;
                triggerGearsetChange();
            }
        }
        public int? Priority
        {
            get => priority;
            set
            {
                if (priority == value)
                    return;

                priority = value;
                triggerGearsetChange();
            }
        }
        public DateTime ImportDate
        {
            get => importDate;
            set
            {
                if (importDate == value)
                    return;

                importDate = value.ToUniversalTime();
                triggerGearsetChange();
            }
        }
        public IReadOnlyList<Gearpiece> Gearpieces
        {
            get => gearpieces;
            init
            {
                foreach (var gearpiece in value)
                    gearpiece.OnGearpieceChange += triggerGearsetChange;

                gearpieces = value;
            }
        }
        // for links to externally sourced sites
        public string? SourceUrl { get; init; }
        // for local representations of the gearset that aren't native JSON (ex: teamcraft plaintext)
        public string? SourceString { get; init; }
        public ImportGearsetSourceType? SourceType { get; init; }
        public string JobAbbrv { get; init; }
        public HighlightColor? HighlightColor
        {
            get => highlightColor;
            set
            {
                // same color
                if (HighlightColor?.BaseColor == value?.BaseColor)
                    return;

                // existing gearset-specific highlight color
                if (HighlightColor is HighlightColor oldColor)
                    if (value is HighlightColor color)
                        oldColor.UpdateColor(color.BaseColor);
                    else
                    {
                        oldColor.OnColorChange -= triggerGearsetChange;
                        highlightColor = null;
                        // changing to new binding (null), trigger update
                        triggerGearsetChange();
                    }
                // uses default highlight color
                else
                {
                    if (value is HighlightColor color)
                    {
                        highlightColor = color;
                        highlightColor!.OnColorChange += triggerGearsetChange;
                        // changing to new binding, trigger update
                        triggerGearsetChange();
                    }
                }
            }
        }

        public event GearsetChangeHandler? OnGearsetChange;

        public Gearset(
            string name,
            List<Gearpiece> gearpieces,
            string jobAbbrv,
            ImportGearsetSourceType? sourceType,
            bool isActive = true,
            string? id = null,
            string? sourceUrl = null,
            string? sourceString = null,
            int priority = 0,
            DateTime? importDate = null
            )
        {
            this.id = id ?? Guid.NewGuid().ToString();
            this.isActive = isActive;
            this.name = name;
            SourceUrl = sourceUrl;
            SourceString = sourceString;
            SourceType = sourceType;
            JobAbbrv = jobAbbrv;
            this.priority = priority;

            // ensure ordering after adding
            gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));
            this.gearpieces = gearpieces;

            this.importDate = importDate ?? DateTime.UtcNow;
        }

        [JsonConstructor]
        public Gearset(
            string id,
            bool isActive,
            string name,
            IReadOnlyList<Gearpiece> gearpieces,
            string jobAbbrv,
            ImportGearsetSourceType? sourceType,
            string? sourceUrl,
            string? sourceString,
            int? priority,
            DateTime importDate,
            HighlightColor? highlightColor
            )
        {
            this.id = id;
            this.isActive = isActive;
            this.name = name;
            SourceUrl = sourceUrl;
            SourceString = sourceString;
            SourceType = sourceType;
            JobAbbrv = jobAbbrv;
            this.priority = priority;
            this.importDate = importDate;
            this.highlightColor = highlightColor;
            this.gearpieces = gearpieces;
        }


        ~Gearset()
        {
            foreach (var gearpiece in Gearpieces)
                gearpiece.OnGearpieceChange -= triggerGearsetChange;
        }

        private void triggerGearsetChange()
        {
            OnGearsetChange?.Invoke();
        }

        public IEnumerable<ItemRequirement> ItemRequirements(bool includeUncollectedItemMateria)
        {
            if (!IsActive)
                yield break;

            foreach (var gearpiece in Gearpieces)
                foreach (var requirement in gearpiece.ItemRequirements(this, includeUncollectedItemMateria))
                    yield return requirement;
        }

        public static IEnumerable<Gearpiece> GetGearpiecesFromGearsets(IEnumerable<Gearset> gearsets)
        {
            return gearsets.SelectMany(g => g.Gearpieces);
        }
    }
}
