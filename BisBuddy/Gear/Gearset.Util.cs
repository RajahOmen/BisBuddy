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
                    // this gearpiece doesn't need this item
                    if (gearpiece.NeedsItemId(itemId, false, true) == 0)
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

        public static HashSet<string> GetUnmeldedItemNames(List<Gearset> gearsets)
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
                    if (gearpiece.PrerequisiteTree != null)
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
