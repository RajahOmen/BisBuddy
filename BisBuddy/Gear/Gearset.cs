using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using System.Runtime.InteropServices;

namespace BisBuddy.Gear
{
    public delegate void GearsetChangeHandler();

    [Serializable]
    public partial class Gearset
    {
        public static readonly string DefaultName = "New Gearset";

        // set to random uuid
        public string Id { get; private set; } = Guid.NewGuid().ToString();
        public bool IsActive { get; private set; } = true;
        public string Name { get; private set; } = "New Gearset";
        public IReadOnlyList<Gearpiece> Gearpieces { get; init; } = [];
        // for links to externally sourced sites
        public string? SourceUrl { get; init; } = null;
        // for local representations of the gearset that aren't native JSON (ex: teamcraft plaintext)
        public string? SourceString { get; init; } = null;
        public ImportGearsetSourceType? SourceType { get; init; }
        public string JobAbbrv { get; init; } = "???";
        public HighlightColor? HighlightColor { get; private set; } = null;

        public event GearsetChangeHandler? OnGearsetChange;

        public Gearset(string name, string? sourceUrl, ImportGearsetSourceType? sourceType)
        {
            Name = name;
            SourceUrl = sourceUrl;
            SourceType = sourceType;
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

            foreach (var gearpiece in Gearpieces)
                gearpiece.OnGearpieceChange += triggerGearsetChange;
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

        public void SetHighlightColor(Vector4? newColor)
        {
            // same color
            if (HighlightColor?.BaseColor == newColor)
                return;

            // existing gearset-specific highlight color
            if (HighlightColor is HighlightColor oldColor)
                if (newColor is Vector4 color)
                    HighlightColor.UpdateColor(color);
                else
                {
                    // changing to new binding, trigger update
                    HighlightColor = oldColor;
                    triggerGearsetChange();
                }
            // uses default highlight color
            else
            {
                if (newColor is Vector4 color)
                {
                    HighlightColor = new HighlightColor(color);
                    // changing to new binding, trigger update
                    triggerGearsetChange();
                }
            }
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
