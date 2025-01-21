using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public partial class Gearset
    {
        public static List<(Gearset gearset, int countNeeded)> GetGearsetsNeedingItemById(uint itemId, List<Gearset> gearsets, bool includeCollectedPrereqs = false)
        {
            // includeCollectedPrereqs: true  - for internal sources: highlight them in inventory to show they are needed to upgrade with
            //                          false - for external sources: don't highlight, we already have enough in inventories
            var satisfiedGearsets = new List<(Gearset gearset, int countNeeded)>();
            // get a dict of gearsets that are satisfied
            foreach (var gearset in gearsets)
            {
                if (!gearset.IsActive) continue;

                var satisfiedGearpieces = gearset.GetGearpiecesNeedingItem(itemId, includeCollectedPrereqs);
                if (satisfiedGearpieces.Count > 0)
                {
                    satisfiedGearsets.Add((gearset, satisfiedGearpieces.Sum(g => g.countNeeded)));
                }
            }

            return satisfiedGearsets;
        }

        public static bool IsItemIncompleteByName(string itemName, List<Gearset> gearsets)
        {
            foreach (var gearset in gearsets)
            {
                if (!gearset.IsActive) continue;

                foreach (var gearpiece in gearset.Gearpieces)
                {
                    if (gearpiece.ItemName != itemName) continue;

                    if (!gearpiece.IsCollected) return true;

                    foreach (var materia in gearpiece.ItemMateria)
                    {
                        if (!materia.IsMelded) return true;
                    }
                }
            }

            return false;
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
                    // item we have isn't this gearpiece
                    if (gearpiece.ItemId != itemId) continue;

                    // look at what materia is needed for this gearpiece
                    var meldsForThisGearpiece = new List<uint>();
                    foreach (var materia in gearpiece.ItemMateria)
                    {
                        if (materia.IsMelded) continue;

                        meldsForThisGearpiece.Add(materia.ItemId);
                    }

                    // if there are any melds needed, add this 'meld plan' to the list
                    if (meldsForThisGearpiece.Count > 0)
                    {
                        var meldPlan = new MeldPlan()
                        {
                            Gearset = gearset,
                            Gearpiece = gearpiece,
                            MateriaIds = meldsForThisGearpiece
                        };
                        neededMeldPlans.Add(meldPlan);
                    }
                }
            }

            // only return distinct meld plans
            var distinctMeldPlans = neededMeldPlans
                .GroupBy(plan => plan.MateriaIds != null
                                 ? string.Join(",", plan.MateriaIds.Distinct().OrderBy(id => id))
                                 : "")
                .Select(group => group.First())
                .ToList();

            return distinctMeldPlans;
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
                    // can't need melds if item isn't collected
                    if (!gearpiece.IsCollected) continue;
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
