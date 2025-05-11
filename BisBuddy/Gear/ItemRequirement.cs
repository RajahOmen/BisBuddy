

namespace BisBuddy.Gear
{
    /// <summary>
    /// Represents how an item may be required by a particular gearpiece.
    /// IsCollected records if the requirement being satisfied by the ItemId is marked as collected
    /// RequirementType represents how the item is needed (be it the gearpiece itself, as a prerequisite, or as a materia)
    /// </summary>
    public struct ItemRequirement
    {
        /// <summary>
        /// The ItemId of the item required
        /// </summary>
        public uint ItemId;

        /// <summary>
        /// The Gearset that requires this item
        /// </summary>
        public Gearset Gearset;

        /// <summary>
        /// The Gearpiece in the gearset that requires this item
        /// </summary>
        public Gearpiece Gearpiece;

        /// <summary>
        /// If the requirement being satisfied by the ItemId is marked as collected
        /// </summary>
        public bool IsCollected;


        /// <summary>
        /// If the requirement being satisfied by the ItemId is marked as obtainable
        /// </summary>
        public bool IsObtainable;

        /// <summary>
        /// How is this item needed (gearpiece, prerequisite piece, materia)
        /// </summary>
        public RequirementType RequirementType;
    }
}
