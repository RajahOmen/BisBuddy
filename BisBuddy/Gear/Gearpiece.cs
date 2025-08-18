using BisBuddy.Gear.Prerequisites;
using BisBuddy.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public delegate void GearpieceChangeHandler();

    [Serializable]
    public class Gearpiece
    {
        private readonly ITypedLogger<Gearpiece> logger;
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public IPrerequisiteNode? PrerequisiteTree { get; set; }
        public bool IsCollected { get; private set; }
        public bool IsManuallyCollected { get; private set; }
        public bool IsObtainable => PrerequisiteTree?.IsObtainable ?? false; // If no prerequisites known, assume not obtainable
        public List<Materia> ItemMateria { get; init; }
        private List<(Materia Materia, int Count)>? itemMateriaGrouped = null;

        public Gearpiece(
            ITypedLogger<Gearpiece> logger,
            uint itemId,
            string itemName,
            GearpieceType gearpieceType,
            IPrerequisiteNode? prerequisiteTree,
            List<Materia>? itemMateria,
            bool isCollected = false,
            bool isManuallyCollected = false
        )
        {
            this.logger = logger;
            ItemId = itemId;
            ItemName = itemName;
            GearpieceType = gearpieceType;
            PrerequisiteTree = prerequisiteTree;
            IsCollected = isCollected;
            IsManuallyCollected = isManuallyCollected;
            ItemMateria = itemMateria ?? [];

            if (PrerequisiteTree is IPrerequisiteNode node)
                node.OnPrerequisiteChange += triggerGearpieceChange;
        }

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

        public event GearpieceChangeHandler? OnGearpieceChange;

        private void triggerGearpieceChange() =>
            OnGearpieceChange?.Invoke();

        public IEnumerable<ItemRequirementOwned> ItemRequirements(Gearset parentGearset, bool includeUncollectedItemMateria)
        {
            yield return new ItemRequirementOwned(
                new(
                    ItemId,
                    IsCollected || IsManuallyCollected,
                    IsObtainable,
                    RequirementType.Gearpiece
                    ),
                parentGearset,
                this
                );

            // ignore materia if uncollected item materia is not enabled
            if (IsCollected || IsManuallyCollected || includeUncollectedItemMateria)
            {
                foreach (var materia in ItemMateria)
                {
                    yield return new ItemRequirementOwned(
                        new(
                            materia.ItemId,
                            materia.IsMelded,
                            false, // materia has no prerequisites
                            RequirementType.Materia
                            ),
                        parentGearset,
                        this
                        );
                }
            }

            if (!IsCollected && PrerequisiteTree is not null)
            {
                var prerequisiteRequirements = PrerequisiteTree.ItemRequirements;
                foreach (var requirement in prerequisiteRequirements)
                    yield return new(
                        requirement,
                        parentGearset,
                        this
                        );
            }
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            if (IsManuallyCollected && !collected && !manualToggle)
            {
                logger.Error($"Cannot automatically uncollect manually collected item: {ItemName}");
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

            triggerGearpieceChange();
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
                    triggerGearpieceChange();
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

            triggerGearpieceChange();
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
                    triggerGearpieceChange();
                    break;
                }
            }
        }

        public void UnmeldAllMateria()
        {
            itemMateriaGrouped = null;
            foreach (var materia in ItemMateria)
                materia.IsMelded = false;

            triggerGearpieceChange();
        }
    }
}
