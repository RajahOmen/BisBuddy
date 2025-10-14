using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Services;
using FFXIVClientStructs.FFXIV.Common.Lua;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public delegate void GearpieceChangeHandler();

    [Serializable]
    public class Gearpiece : ICollectableItem
    {
        private bool isCollected;
        private bool collectLock;
        private readonly ITypedLogger<Gearpiece> logger;
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public IPrerequisiteNode? PrerequisiteTree { get; set; }
        public bool IsCollected {
            get => isCollected;
            set {
                if (CollectLock)
                {
                    logger.Warning($"Attempted to {(value ? "collect" : "uncollect")} locked gearpiece \"{ItemName}\"");
                    return;
                }

                isCollected = value;

                handleIsCollectedChange();
            }
        }
        public bool CollectLock {
            get => collectLock;
            set
            {
                if (collectLock == value)
                    return;

                logger.Info($"{(value ? "locking" : "unlocking")} gearpiece \"{ItemName}\"");
                collectLock = value;
                foreach (var materia in ItemMateria)
                    materia.CollectLock = value;
                triggerGearpieceChange();
            }
        }
        public MateriaGroup ItemMateria { get; init; }

        public Gearpiece(
            ITypedLogger<Gearpiece> logger,
            uint itemId,
            string itemName,
            GearpieceType gearpieceType,
            IPrerequisiteNode? prerequisiteTree,
            MateriaGroup? itemMateria,
            bool isCollected = false,
            bool collectLock = false
        )
        {
            this.logger = logger;
            ItemId = itemId;
            ItemName = itemName;
            GearpieceType = gearpieceType;
            PrerequisiteTree = prerequisiteTree;
            this.isCollected = isCollected;
            this.collectLock = collectLock;
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
                if (PrerequisiteTree is IPrerequisiteNode tree
                    && tree.CollectionStatus >= CollectionStatusType.Obtainable)
                    return CollectionStatusType.Obtainable;
                return CollectionStatusType.NotObtainable;
            }
        }

        private void handleIsCollectedChange()
        {
            if (!IsCollected)
                ItemMateria.UnmeldAllMateria();

            if (PrerequisiteTree != null)
                PrerequisiteTree.IsCollected = IsCollected;

            triggerGearpieceChange();
        }

        public event GearpieceChangeHandler? OnGearpieceChange;

        private void triggerGearpieceChange() =>
            OnGearpieceChange?.Invoke();
            
        public IEnumerable<ItemRequirementOwned> ItemRequirements(Gearset parentGearset, bool includeUncollectedItemMateria)
        {
            yield return new ItemRequirementOwned(
                new(
                    ItemId,
                    CollectionStatus,
                    RequirementType.Gearpiece
                    ),
                parentGearset,
                this
                );

            // ignore materia if uncollected item materia is not enabled
            if (IsCollected || includeUncollectedItemMateria)
            {
                foreach (var materia in ItemMateria)
                {
                    yield return new ItemRequirementOwned(
                        new(
                            materia.ItemId,
                            materia.CollectionStatus,
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

        public void SetIsCollectedLocked(bool toCollect)
        {
            if (toCollect == IsCollected && CollectLock)
                return;

            if (!CollectLock)
                CollectLock = true;

            logger.Info($"Gearpiece \"{ItemName}\" locked as {(toCollect ? "collected" : "uncollected")}");

            isCollected = toCollect;
        }

        public List<uint> CollectLockItemIds()
        {
            if (CollectLock)
                return [ItemId];

            return PrerequisiteTree?.CollectLockItemIds() ?? [];
        }
    }
}
