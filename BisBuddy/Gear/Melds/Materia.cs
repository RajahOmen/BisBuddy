using BisBuddy.Services;
using System;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Gear.Melds
{
    public delegate void MateriaChangeHandler();

    public class Materia(
        ITypedLogger<Materia> logger,
        IAttributeService attributeService,
        MateriaDetails materiaDetails,
        bool isCollected,
        bool collectLock = false
        ) : ICollectableItem
    {
        private readonly ITypedLogger<Materia> logger = logger;

        private bool isCollected = isCollected;
        private bool collectLock = collectLock;

        public readonly uint ItemId = materiaDetails.ItemId;
        public readonly string ItemName = materiaDetails.ItemName;
        public readonly MateriaStatType StatType = materiaDetails.StatType;
        public readonly string StatFullName = materiaDetails.StatName;
        public readonly int MateriaLevel = materiaDetails.Level;
        public readonly int StatQuantity = materiaDetails.Strength;
        public readonly string StatStrength = buildStatStrengthText(
            attributeService, materiaDetails.StatType, materiaDetails.Strength
            );
        public bool IsCollected
        {
            get => isCollected;
            set
            {
                if (isCollected == value)
                    return;

                if (CollectLock)
                {
                    logger.Warning($"Cannot {(value ? "collect" : "uncollect")} materia {ItemId}, is locked.");
                    return;
                }

                isCollected = value;
                OnMateriaChange?.Invoke();
            }
        }

        public bool CollectLock
        {
            get => collectLock;
            set
            {
                if (collectLock == value)
                    return;

                logger.Info($"{(value ? "locking" : "unlocking")} materia \"{ItemName}\"");

                collectLock = value;
                OnMateriaChange?.Invoke();
            }
        }

        public void SetIsCollectedLocked(bool toCollect)
        {
            if (IsCollected == toCollect)
                return;

            if (!CollectLock)
                CollectLock = toCollect;

            logger.Info($"Materia \"{ItemName}\" locked as {(toCollect ? "collected" : "uncollected")}");

            isCollected = toCollect;
        }

        public event MateriaChangeHandler? OnMateriaChange;

        public CollectionStatusType CollectionStatus => IsCollected
            ? CollectionStatusType.ObtainedComplete
            : CollectionStatusType.NotObtainable;

        private static string buildStatStrengthText(
            IAttributeService attributeService,
            MateriaStatType statType,
            int statStrength
            )
        {
            var statAbbreviation = attributeService
                .GetEnumAttribute<DisplayAttribute>(statType)!
                .GetShortName()!;
            return $"+{statStrength} {statAbbreviation}";
        }
    }
}
