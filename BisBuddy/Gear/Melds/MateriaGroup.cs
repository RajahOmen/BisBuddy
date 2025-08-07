using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Melds
{
    public delegate void MateriaGroupChangeHandler();

    /// <summary>
    /// Represents a collection of materia that should be melded onto an item
    /// </summary>
    public class MateriaGroup : ICollection<Materia>
    {
        // maximum length of gearset name for a plan
        public static readonly int MaxMeldPlanNameLength = 30;
        // string appended to unmelded materia in the meld plan
        public static readonly string UnmeldedColorblindIndicator = "*";

        private List<(Materia Type, int Count)> statusGroups = [];
        private List<(string MateriaText, bool IsMelded)> materiaInfo = [];
        private readonly List<Materia> materiaList;

        public MateriaGroup(IEnumerable<Materia>? materia = null)
        {
            materiaList = materia?.ToList() ?? [];
            foreach (var mat in materiaList)
                mat.OnMateriaChange += handleOnMateriaChange;

            // initialize cached values
            handleOnMateriaChange();
        }

        public event MateriaGroupChangeHandler? OnMateriaGroupChange;

        private void triggerMateriaGroupChange()
        {
            OnMateriaGroupChange?.Invoke();
        }

        public IReadOnlyList<(Materia Type, int Count)> StatusGroups =>
            statusGroups;

        public IReadOnlyList<(string MateriaText, bool IsMelded)> MateriaInfo =>
            materiaInfo;

        public int Count =>
            materiaList.Count;

        public bool IsReadOnly =>
            true;

        /// <summary>
        /// Handles the event when a materia changes.
        /// Updates all cached collections that reference materia data that can mutate.
        /// </summary>
        private void handleOnMateriaChange()
        {
            updateStatusGroups();
            updateMateriaInfo();
            triggerMateriaGroupChange();
        }

        private void updateStatusGroups()
        {
            statusGroups = materiaList
                .GroupBy(m => (m.ItemId, m.IsMelded))
                .OrderBy(g => g.Key.IsMelded)
                .Select(g => (g.First(), g.Count()))
                .ToList();
        }

        private void updateMateriaInfo()
        {
            materiaInfo = materiaList
                .Select(m => (
                    // MateriaText
                    // TODO: USE SHORT NAME HERE
                    $"+{m.StatQuantity} {m.StatFullName}{(m.IsMelded ? "" : UnmeldedColorblindIndicator)}",
                    // IsMelded
                    m.IsMelded
                    ))
                .ToList();
        }

        public bool MateriaListCanSatisfy(IEnumerable<Materia> availableMateria)
        {
            // none required, any list satisfies
            if (materiaList.Count == 0)
                return true;

            var availableList = availableMateria.ToList();

            // no restrictions on source, thus list satisfies
            if (availableList.Count == 0)
                return true;

            // returns true if the availableList has all the materia in the requiredList, false otherwise
            var remainingList = materiaList.Select(m => m.ItemId).ToList();

            for (var i = 0; i < availableList.Count; i++)
                remainingList.Remove(availableList[i].ItemId);

            return remainingList.Count == 0;
        }

        public void MeldSingleMateria(uint materiaId)
        {
            for (var i = 0; i < materiaList.Count; i++)
            {
                var materia = materiaList[i];
                if (materia.ItemId == materiaId && !materia.IsMelded)
                {
                    materia.IsMelded = true;
                    materiaList[i] = materia;
                    break;
                }
            }
        }

        public int MeldMultipleMateria(IEnumerable<Materia> materiaToMeld)
        {
            // copy, since this will be modified here
            var materiaIdsToMeld = new List<uint>(materiaToMeld.Select(m => m.ItemId).ToList());

            var slottedCount = 0;

            // iterate over gearpiece slots
            for (var gearIdx = 0; gearIdx < materiaList.Count; gearIdx++)
            {
                var materiaSlot = materiaList[gearIdx];
                var assignedIdx = -1;

                // iterate over candidate materia list
                for (var candidateIdx = 0; candidateIdx < materiaIdsToMeld.Count; candidateIdx++)
                {
                    var candidateItemId = materiaIdsToMeld[candidateIdx];

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
                    materiaIdsToMeld.RemoveAt(assignedIdx);
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
            foreach (var materia in materiaList)
            {
                if (materia.ItemId == materiaId && materia.IsMelded)
                {
                    materia.IsMelded = false;
                    break;
                }
            }
        }

        public void UnmeldAllMateria()
        {
            foreach (var materia in materiaList)
                materia.IsMelded = false;
        }

        public List<Materia> GetMatchingMateria(IEnumerable<Materia> materiaToCompare)
        {
            var newMateriaIdList = materiaToCompare.Select(m => m.ItemId).ToList();
            var matchingMateria = new List<Materia>();

            foreach (var materia in materiaList)
            {
                for (var i = 0; i < newMateriaIdList.Count; i++)
                {
                    if (newMateriaIdList[i] == materia.ItemId)
                    {
                        newMateriaIdList.RemoveAt(i);
                        matchingMateria.Add(materia);
                        break;
                    }
                }
            }

            return matchingMateria;
        }

        public void Add(Materia item) =>
            materiaList.Add(item);

        public void Clear() =>
            materiaList.Clear();

        public bool Contains(Materia item) =>
            materiaList.Contains(item);

        public void CopyTo(Materia[] array, int arrayIndex) =>
            materiaList.CopyTo(array, arrayIndex);

        public bool Remove(Materia item) =>
            materiaList.Remove(item);

        public IEnumerator<Materia> GetEnumerator() =>
            materiaList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            materiaList.GetEnumerator();
    }
}
