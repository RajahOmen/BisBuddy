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
            List<GearpiecePrerequesite>? prerequisiteItems,
            List<Materia>? itemMateria,
            bool isCollected = false
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            GearpieceType = gearpieceType;
            PrerequisiteItems = prerequisiteItems ?? [];
            ItemMateria = itemMateria ?? [];
            IsCollected = isCollected;
        }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public List<GearpiecePrerequesite> PrerequisiteItems { get; set; } // List of item ids that are required to obtain this item
        public bool IsCollected { get; private set; }
        public bool IsManuallyCollected { get; set; } = false; // If this item was manually marked as collected
        public bool IsObtainable => PrerequisiteItems.All(p => p.IsCollected);
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
                    .ThenByDescending(g => g.First().StatQuantity)
                    .ThenBy(g => g.First().StatShortName)
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
            if (manualToggle) IsManuallyCollected = collected;

            // if not manually collected, uncollecting item will unmeld all materia
            if (!collected && !IsManuallyCollected)
            {
                ItemMateria.ForEach(m => m.IsMelded = false);
            }

            // if collected, mark all prerequisites as collected
            if (collected) PrerequisiteItems.ForEach(p => p.SetCollected(collected, manualToggle));

            // if clicked by user as uncollected, uncollect all prerequesites as well
            if (!collected && manualToggle) PrerequisiteItems.ForEach(p => p.SetCollected(false, manualToggle));
        }


        // return the number of this item needed for this gearpiece
        public int NeedsItemId(uint id, bool ignoreCollected, bool includeCollectedPrereqs)
        {
            // Calculate how many of this item are needed as materia (assume item is materia)
            var neededAsMateriaCount = ItemMateria
                .Where(materia => (!materia.IsMelded || !ignoreCollected) && id == materia.ItemId)
                .Count();

            // If this item is marked as collected, only way item is needed is if it is materia
            if (IsCollected && ignoreCollected) return neededAsMateriaCount;

            // is this the item we need
            if (id == ItemId) return 1;

            // Calculate how many of this item are needed as prereqs
            var neededAsPrereqCount =
                includeCollectedPrereqs
                ? PrerequisiteItems.Where(i => i.ItemId == id).Count()
                : PrerequisiteItems.Where(i => i.ItemId == id && (!i.IsCollected || !ignoreCollected)).Count();

            // If the item is one of the prerequisites. If not, returns 0
            return neededAsMateriaCount + neededAsPrereqCount;
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

        public int MeldMultipleMateria(List<uint> materiaList)
        {
            itemMateriaGrouped = null;

            // copy, since this will be modified here
            materiaList = new List<uint>(materiaList);

            var slottedCount = 0;

            // iterate over gearpiece slots
            for (var gearIdx = 0; gearIdx < ItemMateria.Count; gearIdx++)
            {
                var materiaSlot = ItemMateria[gearIdx];
                var assignedIdx = -1;

                // iterate over candidate materia list
                for (var candidateIdx = 0; candidateIdx < materiaList.Count; candidateIdx++)
                {
                    var candidateItemId = materiaList[candidateIdx];

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
                    materiaList.RemoveAt(assignedIdx);
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
