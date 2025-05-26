using BisBuddy.Gear;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Gearsets
{
    public partial class GearsetsService
    {
        /// <summary>
        /// Builds the lookup for every item id that is required of these gearsets and how
        /// </summary>
        private void updateItemRequirements()
        {
            var requirements = new Dictionary<uint, List<ItemRequirement>>();
            foreach (var gearset in currentGearsets)
            {
                foreach (var requirement in gearset.ItemRequirements(configurationService.HighlightUncollectedItemMateria))
                {
                    if (requirements.TryGetValue(requirement.ItemId, out var itemIdRequirements))
                        itemIdRequirements.Add(requirement);
                    else
                        requirements[requirement.ItemId] = [requirement];
                }
            }
            currentItemRequirements = requirements;
        }

        /// <summary>
        /// Whether a list of ItemRequirements include specific item id in some way (as the gearpiece, as a gearpiece prerequisite, or as a materia).
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <param name="itemRequirements">The dictionary of ItemRequirements to check if the item is needed in</param>
        /// <param name="includeMateria">Whether to include requirements of type Materia</param>
        /// <param name="includePrereqs">Whether to include requirements of type Prerequisite</param>
        /// <param name="includeCollected">Whether to include sources that are marked as collected when checking needed status</param>
        /// <param name="includeObtainable">Whether to include sources that are marked as obtainable when checking needed status</param>
        /// <param name="includeCollectedPrereqs">Whether to include prerequisites marked as collected. True for 'internal' inventory sources.
        /// False for 'external' sources outside of player inventory</param>
        /// <returns>If the item is needed in any way by any of the gearsets listed</returns>
        public bool RequirementsNeedItemId(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
            )
        {
            var itemIdRequirements = GetItemRequirements(
                itemId,
                includePrereqs,
                includeMateria,
                includeCollected,
                includeObtainable,
                includeCollectedPrereqs
                );

            return itemIdRequirements.Any();
        }

        /// <summary>
        /// Get list of ItemRequirements for a specific item id. Returns empty list if not needed.
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <param name="itemRequirements">The dictionary of ItemRequirements to check if the item is needed in</param>
        /// <param name="includeMateria">Whether to include requirements of type Materia</param>
        /// <param name="includePrereqs">Whether to include requirements of type Prerequisite</param>
        /// <param name="includeCollected">Whether to include gearpieces that are marked as collected when checking needed status</param>
        /// <param name="includeObtainable">Whether to include sources that are marked as obtainable when checking needed status</param>
        /// <param name="includeCollectedPrereqs">If includePrereqs is true, whether to include prerequisites marked as collected.
        /// True for 'internal' inventory sources. False for 'external' sources outside of player inventory</param>
        /// <returns>List of the items ItemRequirements that match the filters</returns>
        public IEnumerable<ItemRequirement> GetItemRequirements(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
            )
        {
            if (!currentItemRequirements.TryGetValue(itemId, out var itemIdRequirements))
                yield break;

            if (itemIdRequirements.Count == 0)
                yield break;

            foreach (var req in itemIdRequirements)
            {
                if (!includeObtainable && req.IsObtainable && !req.IsCollected)
                    continue;

                switch (req.RequirementType)
                {
                    case RequirementType.Gearpiece:
                        if (includeCollected || !req.IsCollected)
                            yield return req;
                        break;
                    case RequirementType.Materia:
                        if (includeMateria && (includeCollected || !req.IsCollected))
                            yield return req;
                        break;
                    case RequirementType.Prerequisite:
                        if (includePrereqs && (includeCollectedPrereqs || !req.IsCollected))
                            yield return req;
                        break;
                }
            }

            yield break;
        }

        public HighlightColor? GetRequirementColor(
            IEnumerable<ItemRequirement> itemRequirements
            )
        {
            if (!itemRequirements.Any())
                return null;

            var defaultColor = configurationService.DefaultHighlightColor;
            HighlightColor? currentColor = null;
            foreach (var itemRequirement in itemRequirements)
            {
                if (itemRequirement.Gearset.HighlightColor != null)
                {
                    // multiple colors for this requirement, return the default one
                    if (currentColor != null && !currentColor.Equals(itemRequirement.Gearset.HighlightColor))
                        return defaultColor;

                    currentColor = itemRequirement.Gearset.HighlightColor;
                }
                else
                {
                    currentColor = defaultColor;
                }
            }

            // return the gearset's color, or default if no gearset had custom color
            return currentColor ?? defaultColor;
        }

        public HighlightColor? GetRequirementColor(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
            )
        {
            // retrieve list of item requirements for this item id
            var itemIdRequirements = GetItemRequirements(
                itemId,
                includePrereqs,
                includeMateria,
                includeCollected,
                includeObtainable,
                includeCollectedPrereqs
                );

            return GetRequirementColor(itemIdRequirements);
        }

        public List<MeldPlan> GetNeededItemMeldPlans(uint itemId)
        {
            // get item requirements, a max of one per gearpiece
            var itemIdRequirements = GetItemRequirements(
                itemId,
                includePrereqs: configurationService.HighlightPrerequisiteMateria,
                includeMateria: false,
                includeCollected: true,
                includeObtainable: true,
                includeCollectedPrereqs: true
                ).DistinctBy(requirement => requirement.Gearpiece)
                .ToList();

            var neededMeldPlans = new List<MeldPlan>();

            foreach (var requirement in itemIdRequirements)
                // if there are any melds needed, add this 'meld plan' to the list
                if (requirement.Gearpiece.ItemMateria.Any(m => !m.IsMelded))
                    neededMeldPlans.Add(new MeldPlan(requirement.Gearset, requirement.Gearpiece, requirement.Gearpiece.ItemMateria));

            return neededMeldPlans;
        }

        public Dictionary<string, HighlightColor> GetUnmeldedMateriaColors()
        {
            // return list of meldable item names for gearpieces that aren't fully melded
            var itemNames = new Dictionary<string, HighlightColor>();

            foreach (var gearset in currentGearsets)
            {
                if (!gearset.IsActive) continue;
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // all gearpiece materia is melded, no melds required
                    if (gearpiece.ItemMateria.All(m => m.IsMelded))
                        continue;

                    HashSet<string> newItemNames;
                    if (gearpiece.PrerequisiteTree is not null && configurationService.HighlightPrerequisiteMateria)
                    {
                        newItemNames = gearpiece.PrerequisiteTree.MeldableItemNames();
                        newItemNames.Add(gearpiece.ItemName);
                    }
                    else
                    {
                        newItemNames = [gearpiece.ItemName];
                    }

                    foreach (var newItemName in newItemNames)
                    {
                        // add item itself to list
                        if (
                        itemNames.TryGetValue(newItemName, out var currentColor)
                        && !currentColor.Equals(gearset.HighlightColor)
                        ) // multiple colors, use default color
                            itemNames[newItemName] = configurationService.DefaultHighlightColor;
                        else
                            itemNames[newItemName] = gearset.HighlightColor
                                ?? configurationService.DefaultHighlightColor;
                    }
                }
            }

            return itemNames;
        }
    }
}
