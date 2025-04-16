using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public partial class Gearset
    {
        public static List<(Gearset gearset, int countNeeded)> GetGearsetsNeedingItemById(
            uint itemId,
            List<Gearset> gearsets,
            bool ignoreCollected = true,
            bool includeCollectedPrereqs = false
            )
        {
            // includeCollectedPrereqs: true  - for internal sources: highlight them in inventory to show they are needed to upgrade with
            //                          false - for external sources: don't highlight, we already have enough in inventories
            var satisfiedGearsets = new List<(Gearset gearset, int countNeeded)>();
            // get a dict of gearsets that are satisfied
            foreach (var gearset in gearsets)
            {
                if (!gearset.IsActive) continue;

                var satisfiedGearpieces = gearset.GetGearpiecesNeedingItem(itemId, ignoreCollected, includeCollectedPrereqs);
                if (satisfiedGearpieces.Count > 0)
                {
                    satisfiedGearsets.Add((gearset, satisfiedGearpieces.Sum(g => g.countNeeded)));
                }
            }

            return satisfiedGearsets;
        }

        /// <summary>
        /// Whether a list of gearsets need a specific item id in some way (as the gearpiece, as a gearpiece prerequisite, or as a materia).
        /// Ignores gearsets marked as inactive in the list.
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <param name="gearsets">The list of gearsets to check if the item is needed in</param>
        /// <param name="ignoreCollected">Whether to ignore gearpieces that are marked as collected when checking needed status</param>
        /// <param name="includeCollectedPrereqs">Whether to include prerequisites marked as collected. True for 'internal' inventory sources.
        /// False for external sources outside of player inventory</param>
        /// <returns>If the item is needed in any way by any of the gearsets listed</returns>
        public static bool GearsetsNeedItemId(
            uint itemId,
            List<Gearset> gearsets,
            bool ignoreCollected = true,
            bool includeCollectedPrereqs = false,
            bool includeUncollectedItemMateria = true
            ) =>
            gearsets.Any(gearset => gearset.NeedsItemId(itemId, ignoreCollected, includeCollectedPrereqs, includeUncollectedItemMateria));

        public static List<MeldPlan> GetNeededItemMeldPlans(uint itemId, List<Gearset> gearsets, bool includeAsPrerequisite)
        {
            var neededMeldPlans = new List<MeldPlan>();

            // look through all gearsets
            foreach (var gearset in gearsets)
            {
                // only active ones
                if (!gearset.IsActive) continue;

                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // this gearpiece doesn't need this item
                    if (!gearpiece.NeedsItemId(itemId, false, true, true, includeAsPrerequisite: includeAsPrerequisite))
                        continue;

                    // look at what materia is needed for this gearpiece
                    var gearpieceMateria = new List<Materia>();
                    foreach (var materia in gearpiece.ItemMateria)
                        gearpieceMateria.Add(materia);

                    // if there are any melds needed, add this 'meld plan' to the list
                    if (gearpieceMateria.Any(m => !m.IsMelded))
                        neededMeldPlans.Add(new MeldPlan(gearset, gearpiece, gearpieceMateria));
                }
            }

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
