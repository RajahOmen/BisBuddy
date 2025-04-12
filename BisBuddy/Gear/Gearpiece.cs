using BisBuddy.Gear.Prerequisites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    [Serializable]
    public class Gearpiece
    {
        public Gearpiece(
            uint itemId,
            string itemName,
            GearpieceType gearpieceType,
            PrerequisiteNode? prerequisiteTree,
            List<Materia>? itemMateria,
            bool isCollected = false
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            GearpieceType = gearpieceType;
            PrerequisiteTree = prerequisiteTree;
            ItemMateria = itemMateria ?? [];
            IsCollected = isCollected;
        }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public PrerequisiteNode? PrerequisiteTree { get; set; } // relations with other items that can be used to obtain this gearpiece
        public bool IsCollected { get; private set; }
        public bool IsManuallyCollected { get; set; } = false; // If this item was manually marked as collected
        public bool IsObtainable => PrerequisiteTree?.IsObtainable ?? false; // If no prerequisites known, assume not obtainable
        public List<Materia> ItemMateria { get; init; }
        private List<(Materia Materia, int Count)>? itemMateriaGrouped = null;
        public List<(Materia Materia, int Count)>? ItemMateriaGrouped
        {
            get
            {
                itemMateriaGrouped ??= ItemMateria
                    .GroupBy(m => new
                    {
                        m.IsMelded,
                        m.ItemId,
                    }).OrderBy(g => g.Key.IsMelded)
                    .Select(g => (g.First(), g.Count()))
                    .ToList();
                return itemMateriaGrouped;
            }
            set
            {
                itemMateriaGrouped = value;
            }
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            if (IsManuallyCollected && !collected && !manualToggle)
            {
                Services.Log.Error($"Cannot automatically uncollect manually collected item: {ItemName}");
                return;
            }

            IsCollected = collected;

            // if toggled by user, set manually collected flag
            if (manualToggle)
                IsManuallyCollected = collected;

            // if not manually collected, uncollecting item will unmeld all materia
            if (!collected && !IsManuallyCollected)
                ItemMateria.ForEach(m => m.IsMelded = false);

            // if collected, mark all prerequisites as collected
            if (collected && PrerequisiteTree != null)
                PrerequisiteTree.SetCollected(collected, manualToggle);

            // if clicked by user as uncollected, uncollect all prerequisites as well
            if (!collected && manualToggle && PrerequisiteTree != null)
                PrerequisiteTree.SetCollected(false, manualToggle);
        }


        // return the number of this item needed for this gearpiece
        public int NeedsItemId(uint candidateItemId, bool ignoreCollected, bool includeCollectedPrereqs)
        {
            // not a real item, can't be needed
            if (candidateItemId == 0) return 0;

            // Calculate how many of this item are needed as materia (assume item is materia)
            var neededAsMateriaCount = ItemMateria
                .Where(materia => (!materia.IsMelded || !ignoreCollected) && candidateItemId == materia.ItemId)
                .Count();

            // If this item is marked as collected, only way item is needed is if it is materia
            if (IsCollected && ignoreCollected) return neededAsMateriaCount;

            // is this the item we need
            if (candidateItemId == ItemId) return 1;

            // Calculate how many of this item are needed as prereqs
            // if includeCollectedPrereqs: false
            // if not includeCollectedPrereqs: val of ignoreCollected
            var neededAsPrereqCount = PrerequisiteTree?.ItemNeededCount(candidateItemId, !includeCollectedPrereqs && ignoreCollected) ?? 0;

            // If the item is one of the prerequisites. If not, returns 0
            return neededAsMateriaCount + neededAsPrereqCount;
        }

        public List<uint> ManuallyCollectedItemIds()
        {
            if (IsManuallyCollected)
                return [ItemId];

            if (PrerequisiteTree == null)
                return [];

            return PrerequisiteTree.ManuallyCollectedItemIds();
        }

        public void MeldSingleMateria(uint materiaId)
        {
            itemMateriaGrouped = null;
            for (var i = 0; i < ItemMateria.Count; i++)
            {
                var materia = ItemMateria[i];
                if (materia.ItemId == materiaId && !materia.IsMelded)
                {
                    materia.IsMelded = true;
                    ItemMateria[i] = materia;
                    break;
                }
            }
        }

        public int MeldMultipleMateria(List<Materia> materiaList)
        {
            itemMateriaGrouped = null;

            // copy, since this will be modified here
            var materiaIdList = new List<uint>(materiaList.Select(m => m.ItemId).ToList());

            var slottedCount = 0;

            // iterate over gearpiece slots
            for (var gearIdx = 0; gearIdx < ItemMateria.Count; gearIdx++)
            {
                var materiaSlot = ItemMateria[gearIdx];
                var assignedIdx = -1;

                // iterate over candidate materia list
                for (var candidateIdx = 0; candidateIdx < materiaIdList.Count; candidateIdx++)
                {
                    var candidateItemId = materiaIdList[candidateIdx];

                    // gearpiece slot requires this candidate materia id
                    if (candidateItemId == materiaSlot.ItemId)
                    {
                        // materia slot was previously not melded
                        if (!materiaSlot.IsMelded)
                        {
                            // meld candidate piece into slot
                            materiaSlot.IsMelded = true;
                            slottedCount++;
                        }

                        // this candidate is now "filling" this slot. Remove it from further slot assignments
                        assignedIdx = candidateIdx;
                        break;
                    }
                }

                // remove candidate from list if it was assigned
                if (assignedIdx > -1)
                {
                    materiaIdList.RemoveAt(assignedIdx);
                }
                else
                {
                    materiaSlot.IsMelded = false;
                }
            }

            return slottedCount;
        }

        public void UnmeldSingleMateria(uint materiaId)
        {
            itemMateriaGrouped = null;
            for (var i = 0; i < ItemMateria.Count; i++)
            {
                var materia = ItemMateria[i];
                if (materia.ItemId == materiaId && materia.IsMelded)
                {
                    materia.IsMelded = false;
                    ItemMateria[i] = materia;
                    break;
                }
            }
        }
    }
}
