

namespace BisBuddy.Gear;

/// <summary>
/// Represents an <see cref="Gear.ItemRequirement"/> that is representing some need by a specific <see cref="Gear.Gearpiece"/> in a specific <see cref="Gear.Gearset"/>
/// </summary>
/// <param name="ItemRequirement">The item requirement that is owned by a gearpiece/gearset</param>
/// <param name="Gearset">The gearset that owns this requirement/gearpiece</param>
/// <param name="Gearpiece">The gearpiece that owns this requirement</param>
public readonly record struct ItemRequirementOwned(
    ItemRequirement ItemRequirement,
    Gearset Gearset,
    Gearpiece Gearpiece
    );
