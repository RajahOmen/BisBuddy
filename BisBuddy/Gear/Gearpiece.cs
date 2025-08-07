using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public delegate void GearpieceChangeHandler();

    [Serializable]
    public class Gearpiece : ICollectableItem
    {
        private readonly ITypedLogger<Gearpiece> logger;
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public IPrerequisiteNode? PrerequisiteTree { get; set; }
        public bool IsCollected { get; private set; }
        public bool IsManuallyCollected { get; private set; }
        public bool IsObtainable => PrerequisiteTree?.IsObtainable ?? false; // If no prerequisites known, assume not obtainable
        public MateriaGroup ItemMateria { get; init; }

        public Gearpiece(
            ITypedLogger<Gearpiece> logger,
            uint itemId,
            string itemName,
            GearpieceType gearpieceType,
            IPrerequisiteNode? prerequisiteTree,
            MateriaGroup? itemMateria,
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

            ItemMateria.OnMateriaGroupChange += triggerGearpieceChange;
        }

        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return ItemMateria.All(m => m.CollectionStatus == CollectionStatusType.ObtainedComplete)
                        ? CollectionStatusType.ObtainedComplete
                        : CollectionStatusType.ObtainedPartial;
                if (IsObtainable)
                    return CollectionStatusType.Obtainable;
                return CollectionStatusType.NotObtainable;
            }
        }

        public event GearpieceChangeHandler? OnGearpieceChange;

        private void triggerGearpieceChange() =>
            OnGearpieceChange?.Invoke();

        public IEnumerable<ItemRequirement> ItemRequirements(Gearset parentGearset, bool includeUncollectedItemMateria)
        {
            yield return new ItemRequirement()
            {
                ItemId = ItemId,
                Gearset = parentGearset,
                Gearpiece = this,
                IsCollected = IsCollected || IsManuallyCollected,
                IsObtainable = IsObtainable,
                RequirementType = RequirementType.Gearpiece,
            };

            // ignore materia if uncollected item materia is not enabled
            if (IsCollected || IsManuallyCollected || includeUncollectedItemMateria)
            {
                foreach (var materia in ItemMateria)
                {
                    yield return new ItemRequirement()
                    {
                        ItemId = materia.ItemId,
                        Gearset = parentGearset,
                        Gearpiece = this,
                        IsCollected = materia.IsMelded,
                        IsObtainable = false, // materia has no prerequisites
                        RequirementType = RequirementType.Materia,
                    };
                }
            }

            if (!IsCollected && PrerequisiteTree is not null)
            {
                var prerequisiteRequirements = PrerequisiteTree.ItemRequirements(parentGearset, this);
                foreach (var requirement in prerequisiteRequirements)
                    yield return requirement;
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
                ItemMateria.UnmeldAllMateria();

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
    }
}
