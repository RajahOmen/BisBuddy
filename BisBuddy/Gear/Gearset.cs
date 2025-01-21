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

        public List<Gearpiece> UpdateGearpiecesWithItem(uint itemId, List<uint> materiaList)
        {
            List<Gearpiece> updatedGearpieces = [];

            // iterate through user's gearpieces
            foreach (var gearpiece in Gearpieces)
            {
                if (gearpiece.UpdateWithItem(itemId, materiaList))
                {
                    updatedGearpieces.Add(gearpiece);
                }
            }

            return updatedGearpieces;
        }

        public List<Gearpiece> RemoveItemFromGearpieces(uint itemId, List<uint> materiaList)
        {
            var removedGearpieces = new List<Gearpiece>();
            foreach (var gearpiece in Gearpieces)
            {
                if (gearpiece.IsItem(itemId, materiaList)) // is the item being removed
                {
                    gearpiece.SetCollected(false, false);
                    foreach (var materia in gearpiece.ItemMateria) materia.IsMelded = false;
                    removedGearpieces.Add(gearpiece);
                }
            }

            return removedGearpieces;
        }

        public (int, int, int) GetCompletionStatus()
        {
            // generate tuple of completed, materia needed, incompleted status
            var completed = 0;
            var materiaNeeded = 0;
            var incompleted = 0;

            foreach (var gearpiece in Gearpieces)
            {
                if (gearpiece.IsCollected)
                {
                    completed++; // assume fully completed at first
                    foreach (var materia in gearpiece.ItemMateria)
                    {
                        if (!materia.IsMelded)
                        {
                            completed--; // not fully completed, remove from completed
                            materiaNeeded++; // add to materia needed
                            break;
                        }
                    }
                }
                else
                {
                    incompleted++;
                }
            }

            return (completed, materiaNeeded, incompleted);
        }
    }
}
