using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public partial class Gearset
    {
        public static Dictionary<uint, List<ItemRequirement>> BuildItemRequirements(
            List<Gearset> gearsets,
            bool includeUncollectedItemMateria
            )
        {
            var requirements = new Dictionary<uint, List<ItemRequirement>>();

            foreach (var gearset in gearsets)
            {
                foreach (var requirement in gearset.ItemRequirements(includeUncollectedItemMateria))
                {
                    if (requirements.TryGetValue(requirement.ItemId, out var itemIdRequirements))
                        itemIdRequirements.Add(requirement);
                    else
                        requirements[requirement.ItemId] = [requirement];
                }
            }

            return requirements;
        }

        /// <summary>
        /// Whether a list of ItemRequirements include specific item id in some way (as the gearpiece, as a gearpiece prerequisite, or as a materia).
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <param name="itemRequirements">The dictionary of ItemRequirements to check if the item is needed in</param>
        /// <param name="includeMateria">Whether to include requirements of type Materia</param>
        /// <param name="includePrereqs">Whether to include requirements of type Prerequisite</param>
        /// <param name="includeCollected">Whether to include gearpieces that are marked as collected when checking needed status</param>
        /// <param name="includeCollectedPrereqs">Whether to include prerequisites marked as collected. True for 'internal' inventory sources.
        /// False for 'external' sources outside of player inventory</param>
        /// <returns>If the item is needed in any way by any of the gearsets listed</returns>
        public static bool RequirementsNeedItemId(
            uint itemId,
            Dictionary<uint, List<ItemRequirement>> itemRequirements,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeCollectedPrereqs = false
            )
        {
            if (!itemRequirements.TryGetValue(itemId, out var itemIdRequirements))
                return false;

            if (itemIdRequirements.Count == 0)
                return false;

            // no further filtering down, so any requirement is valid
            if (includeCollected && includeCollectedPrereqs)
                return true;

            foreach (var itemRequirement in itemIdRequirements)
            {
                switch (itemRequirement.RequirementType)
                {
                    case RequirementType.Gearpiece:
                        if (!itemRequirement.IsCollected || includeCollected)
                            return true;
                        break;
                    case RequirementType.Materia:
                        if (includeMateria && (!itemRequirement.IsCollected || includeCollected))
                            return true;
                        break;
                    case RequirementType.Prerequisite:
                        if (includePrereqs && (!itemRequirement.IsCollected || includeCollectedPrereqs))
                            return true;
                        break;
                    default:
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// Get list of ItemRequirements for a specific item id. Returns empty list if not needed.
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <param name="itemRequirements">The dictionary of ItemRequirements to check if the item is needed in</param>
        /// <param name="includeMateria">Whether to include requirements of type Materia</param>
        /// <param name="includePrereqs">Whether to include requirements of type Prerequisite</param>
        /// <param name="includeCollected">Whether to include gearpieces that are marked as collected when checking needed status</param>
        /// <param name="includeCollectedPrereqs">If includePrereqs is true, whether to include prerequisites marked as collected.
        /// True for 'internal' inventory sources. False for 'external' sources outside of player inventory</param>
        /// <returns>List of the items ItemRequirements that match the filters</returns>
        public static IReadOnlyList<ItemRequirement> GetItemRequirements(
            uint itemId,
            Dictionary<uint, List<ItemRequirement>> itemRequirements,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeCollectedPrereqs = false
            )
        {
            if (!itemRequirements.TryGetValue(itemId, out var itemIdRequirements))
                return [];

            if (itemIdRequirements.Count == 0)
                return [];

            // no further filtering down, so any requirement is valid
            if (includeCollected && includeCollectedPrereqs)
                return itemIdRequirements;

            var filteredRequirements = itemIdRequirements.Where(requirement =>
                    requirement.RequirementType switch
                    {
                        RequirementType.Gearpiece => !requirement.IsCollected || includeCollected,
                        RequirementType.Materia => includeMateria && (!requirement.IsCollected || includeCollected),
                        RequirementType.Prerequisite => includePrereqs && (!requirement.IsCollected || includeCollectedPrereqs),
                        _ => false
                    }
                ).ToList();

            return filteredRequirements;
        }

        public static List<MeldPlan> GetNeededItemMeldPlans(
            uint itemId,
            Dictionary<uint, List<ItemRequirement>> itemRequirements,
            bool includeAsPrerequisite
            )
        {
            // get item requirements, a max of one per gearpiece
            var itemIdRequirements = GetItemRequirements(
                itemId,
                itemRequirements,
                includePrereqs: includeAsPrerequisite,
                includeMateria: false,
                includeCollected: true,
                includeCollectedPrereqs: true
                ).DistinctBy(requirement => requirement.Gearpiece);

            var neededMeldPlans = new List<MeldPlan>();

            foreach (var requirement in itemIdRequirements)
                // if there are any melds needed, add this 'meld plan' to the list
                if (requirement.Gearpiece.ItemMateria.Any(m => !m.IsMelded))
                    neededMeldPlans.Add(new MeldPlan(requirement.Gearset, requirement.Gearpiece, requirement.Gearpiece.ItemMateria));

            return neededMeldPlans;
        }

        public static HashSet<string> GetUnmeldedItemNames(List<Gearset> gearsets, bool includePrerequisites)
        {
            // return list of meldable item names for gearpieces that aren't fully melded
            var itemNames = new HashSet<string>();

            foreach (var gearset in gearsets)
            {
                if (!gearset.IsActive) continue;
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // all gearpiece materia is melded, no melds required
                    if (gearpiece.ItemMateria.All(m => m.IsMelded))
                        continue;

                    // add item itself to list
                    itemNames.Add(gearpiece.ItemName);

                    // try to find any meldable items in prereq tree and add those names
                    if (gearpiece.PrerequisiteTree != null && includePrerequisites)
                        itemNames.UnionWith(gearpiece.PrerequisiteTree.MeldableItemNames());
                }
            }

            return itemNames;
        }

        public static List<Gearpiece> GetGearpiecesFromGearsets(List<Gearset> sourceGearsets)
        {
            return sourceGearsets.SelectMany(gearset => gearset.Gearpieces).ToList();
        }
    }
}
