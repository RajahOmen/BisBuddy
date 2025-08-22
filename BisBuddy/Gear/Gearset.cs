using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public delegate void GearsetChangeHandler();

    [Serializable]
    public partial class Gearset
    {
        public static readonly string DefaultName = "New Gearset";

        private readonly IReadOnlyList<Gearpiece> gearpieces = [];
        private HighlightColor? highlightColor = null;

        // set to random uuid
        public string Id { get; private set; } = Guid.NewGuid().ToString();
        public bool IsActive { get; private set; } = true;
        public string Name { get; private set; } = "New Gearset";
        public IReadOnlyList<Gearpiece> Gearpieces
        {
            get => gearpieces;
            init
            {
                foreach (var gearpiece in value)
                    gearpiece.OnGearpieceChange += triggerGearsetChange;

                gearpieces = value ?? [];
            }
        }
        // for links to externally sourced sites
        public string? SourceUrl { get; init; } = null;
        // for local representations of the gearset that aren't native JSON (ex: teamcraft plaintext)
        public string? SourceString { get; init; } = null;
        public ImportGearsetSourceType? SourceType { get; init; }
        public string JobAbbrv { get; init; } = "???";
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

        public Gearset(string name, string? sourceUrl, ImportGearsetSourceType? sourceType, bool isActive)
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceType = sourceType;
            IsActive = isActive;
        }

        internal Gearset(
            string name,
            List<Gearpiece> gearpieces,
            string jobAbbrv,
            ImportGearsetSourceType? sourceType,
            string? sourceUrl = null,
            string? sourceString = null
            )
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceString = sourceString;
            SourceType = sourceType;
            JobAbbrv = jobAbbrv;

            // ensure ordering after adding
            gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));
            Gearpieces = gearpieces;
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

        public void SetId(string newId)
        {
            Id = newId;
            triggerGearsetChange();
        }

        public void SetActiveStatus(bool newActivityStatus)
        {
            IsActive = newActivityStatus;
            triggerGearsetChange();
        }

        public void SetName(string newName)
        {
            Name = newName;
            triggerGearsetChange();
        }

        public IEnumerable<ItemRequirementOwned> ItemRequirements(bool includeUncollectedItemMateria)
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
