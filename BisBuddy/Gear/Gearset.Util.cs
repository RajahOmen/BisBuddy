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
            bool includeCollectedPrereqs = false
            ) =>
            gearsets.Any(gearset => gearset.NeedsItemId(itemId, ignoreCollected, includeCollectedPrereqs));

        public static List<MeldPlan> GetNeededItemMeldPlans(uint itemId, List<Gearset> gearsets)
        {
            var neededMeldPlans = new List<MeldPlan>();

            // look through all gearsets
            foreach (var gearset in gearsets)
            {
                // only active ones
                if (!gearset.IsActive) continue;

                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // item we have isn't this gearpiece
                    if (gearpiece.ItemId != itemId) continue;

                    // look at what materia is needed for this gearpiece
                    var gearpieceMateria = new List<Materia>();
                    foreach (var materia in gearpiece.ItemMateria)
                    {
                        gearpieceMateria.Add(materia);
                    }

                    // if there are any melds needed, add this 'meld plan' to the list
                    if (gearpieceMateria.Any(m => !m.IsMelded))
                        neededMeldPlans.Add(new MeldPlan(gearset, gearpiece, gearpieceMateria));
                }
            }

            return neededMeldPlans;
        }

        public static List<Gearpiece> GetUnmeldedGearpieces(List<Gearset> gearsets)
        {
            // return list of gearpieces that are not fully melded
            var gearpieces = new List<Gearpiece>();

            foreach (var gearset in gearsets)
            {
                if (!gearset.IsActive) continue;
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    foreach (var materia in gearpiece.ItemMateria)
                    {
                        if (!materia.IsMelded)
                        {
                            gearpieces.Add(gearpiece);
                            break;
                        }
                    }
                }
            }

            return gearpieces;
        }

        public static List<Gearpiece> GetGearpiecesFromGearsets(List<Gearset> sourceGearsets)
        {
            return sourceGearsets.SelectMany(gearset => gearset.Gearpieces).ToList();
        }
    }
}
