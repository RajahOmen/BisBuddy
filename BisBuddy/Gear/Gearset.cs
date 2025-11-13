using BisBuddy.Import;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public delegate void GearsetChangeHandler(bool effectsAssignments);

    public class Gearset
    {
        private readonly IReadOnlyList<Gearpiece> gearpieces = [];

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
                triggerGearsetChange(effectsAssignments: false);
            }
        }
        public bool IsActive
        {
            get => isActive;
            set
            {
                if (isActive == value)
                    return;

                isActive = value;
                triggerGearsetChange(effectsAssignments: true);
            }
        }
        public string Name
        {
            get => name;
            set
            {
                if (name == value)
                    return;

                name = value ?? string.Empty;
                triggerGearsetChange(effectsAssignments: false);
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
                triggerGearsetChange(effectsAssignments: true);
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
                triggerGearsetChange(effectsAssignments: false);
            }
        }
        public IReadOnlyList<Gearpiece> Gearpieces
        {
            get => gearpieces;
            init
            {
                foreach (var gearpiece in value)
                    gearpiece.OnGearpieceChange += handleChangeWithAssignments;

                gearpieces = value.OrderBy(g => g.GearpieceType).ToList() ?? [];
            }
        }
        // for links to externally sourced sites
        public string? SourceUrl { get; init; }
        // for local representations of the gearset that aren't native JSON (ex: teamcraft plaintext)
        public string? SourceString { get; init; }
        public ImportGearsetSourceType? SourceType { get; init; }
        public ClassJobInfo ClassJobInfo { get; init; }
        public string ClassJobAbbreviation => ClassJobInfo.Abbreviation;
        public string ClassJobName => ClassJobInfo.Name;
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
                        oldColor.OnColorChange -= handleChangeWithoutAssignments;
                        highlightColor = null;
                        // changing to new binding (null), trigger update
                        triggerGearsetChange(effectsAssignments: false);
                    }
                // uses default highlight color
                else
                {
                    if (value is HighlightColor color)
                    {
                        highlightColor = color;
                        highlightColor!.OnColorChange += handleChangeWithoutAssignments;
                        // changing to new binding, trigger update
                        triggerGearsetChange(effectsAssignments: false);
                    }
                }
            }
        }

        public event GearsetChangeHandler? OnGearsetChange;

        public Gearset(
            string id,
            bool isActive,
            string name,
            IReadOnlyList<Gearpiece> gearpieces,
            ClassJobInfo classJobInfo,
            ImportGearsetSourceType? sourceType,
            string? sourceUrl,
            string? sourceString,
            int priority,
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
            ClassJobInfo = classJobInfo;
            Gearpieces = gearpieces;
            this.priority = priority;
            this.importDate = importDate;
            this.highlightColor = highlightColor;
        }


        ~Gearset()
        {
            foreach (var gearpiece in Gearpieces)
                gearpiece.OnGearpieceChange -= handleChangeWithAssignments;
        }

        private void handleChangeWithAssignments() =>
            triggerGearsetChange(effectsAssignments: true);

        private void handleChangeWithoutAssignments() =>
            triggerGearsetChange(effectsAssignments: false);

        private void triggerGearsetChange(bool effectsAssignments) =>
            OnGearsetChange?.Invoke(effectsAssignments);

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
