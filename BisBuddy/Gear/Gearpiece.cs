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
        public List<Materia> ItemMateria { get; set; }

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
        public int NeedsItemId(uint id, bool includeCollectedPrereqs)
        {
            // Calculate how many of this item are needed as materia (assume item is materia)
            var neededAsMateriaCount = ItemMateria
                .Where(materia => !materia.IsMelded && id == materia.ItemId)
                .Count();

            // If this item is marked as collected, only way item is needed is if it is materia
            if (IsCollected) return neededAsMateriaCount;

            // is this the item we need
            if (id == ItemId) return 1;

            // Calculate how many of this item are needed as prereqs
            var neededAsPrereqCount =
                includeCollectedPrereqs
                ? PrerequisiteItems.Where(i => i.ItemId == id).Count()
                : PrerequisiteItems.Where(i => i.ItemId == id && !i.IsCollected).Count();

            // If the item is one of the prerequisites. If not, returns 0
            return neededAsMateriaCount + neededAsPrereqCount;
        }

        public void MeldSingleMateria(uint materiaId)
        {
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
            if (materiaList.Count == 0) return 0;

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
                            ItemMateria[gearIdx] = materiaSlot;
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

                    // materia list exhausted
                    if (materiaList.Count == 0)
                    {
                        break;
                    }
                }
            }

            return slottedCount;
        }

        public void UnmeldSingleMateria(uint materiaId)
        {
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

        public bool UpdateWithItem(uint itemId, List<uint> materiaList)
        {
            if (ItemId != itemId) return false;

            var gearpieceUpdated = false;
            // was this previously not collected?
            if (!IsCollected)
            {
                gearpieceUpdated = true;
                SetCollected(true, false);
                Services.Log.Debug($"Marking \"{ItemName}\" as collected");
            }

            // update materia, if applicable
            if (materiaList.Count > 0)
            {
                var meldedMateriaCount = ItemMateria.Where(m => m.IsMelded).Count();
                var matchingMateria = Materia.GetMatchingMateria(ItemMateria, materiaList);

                // current melds are better/as good as new item, don't replace
                if (meldedMateriaCount > matchingMateria.Count) return gearpieceUpdated;

                // break ties by stat quantity (Ex: keep [+54, +54] over [+36 +36])
                if (meldedMateriaCount == matchingMateria.Count)
                {
                    var meldedMateriaStatTotal = ItemMateria.Sum(m => m.StatQuantity);
                    var matchingMateriaStatTotal = matchingMateria.Sum(m => m.StatQuantity);
                    if (meldedMateriaStatTotal >= matchingMateriaStatTotal) return gearpieceUpdated;
                }

                // gearpiece will be updated
                gearpieceUpdated = true;

                // new melds better than previous, unmeld old materia
                foreach (var materia in ItemMateria) materia.IsMelded = false;

                // meld new materiad
                foreach (var materia in matchingMateria) materia.IsMelded = true;
            }

            return gearpieceUpdated;
        }

        public bool IsItem(uint itemId, List<uint> materiaList)
        {
            if (ItemId != itemId) return false;

            var meldedMateriaList = ItemMateria.Where(m => m.IsMelded).Select(m => m.ItemId).ToList();

            // check if all materia match by sorting and comparing m1[i] == m2[i]
            var eqLists = materiaList.OrderBy(m => m).SequenceEqual(meldedMateriaList.OrderBy(m => m));

            return eqLists && materiaList.Count == meldedMateriaList.Count;
        }
    }
}
