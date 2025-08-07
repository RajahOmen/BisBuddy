using System;

namespace BisBuddy.Gear.Melds
{
    public delegate void MateriaChangeHandler();

    [Serializable]
    public class Materia(
        MateriaDetails materiaDetails,
        bool isMelded
        ) : ICollectableItem
    {
        private bool isMelded = isMelded;

        public readonly uint ItemId = materiaDetails.ItemId;
        public readonly string ItemName = materiaDetails.ItemName;
        public readonly MateriaStatType StatType = materiaDetails.StatType;
        public readonly string StatFullName = materiaDetails.StatName;
        public readonly int MateriaLevel = materiaDetails.Level;
        public readonly int StatQuantity = materiaDetails.Strength;
        public bool IsMelded
        {
            get => isMelded;
            set
            {
                if (isMelded == value)
                    return;
                isMelded = value;
                OnMateriaChange?.Invoke();
            }
        }

        public event MateriaChangeHandler? OnMateriaChange;

        public CollectionStatusType CollectionStatus => IsMelded
            ? CollectionStatusType.ObtainedComplete
            : CollectionStatusType.NotObtainable;
    }
}
