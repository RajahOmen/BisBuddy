

namespace BisBuddy.Gear;

/// <summary>
/// Represents how an item may be required for something.
/// IsCollected records if the requirement being satisfied by the ItemId is marked as collected
/// RequirementType represents how the item is needed (be it the gearpiece itself, as a prerequisite, or as a materia)
/// </summary>
/// <param name="ItemId">The ItemId of the item required</param>
/// <param name="CollectionStatus">The current <see cref="ICollectableItem.CollectionStatus"/> of the requirement being satisfied by the ItemId is marked as collected</param>
/// <param name="IsObtainable">If the requirement being satisfied by the ItemId is marked as obtainable</param>
/// <param name="RequirementType">How is this item needed (gearpiece, prerequisite piece, materia)</param>
public readonly record struct ItemRequirement(
    uint ItemId,
    CollectionStatusType CollectionStatus,
    RequirementType RequirementType
    );
