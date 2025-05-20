using BisBuddy.Gear;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public static class ItemAssigner
    {
        // logic for applying assignments decided by solver

        public static List<Gearpiece> MakeItemAssignments(List<Assignment> assignments, List<Gearpiece> gearpiecesToAssign, ItemDataService itemData)
        {
            var updatedGearpieces = new List<Gearpiece>();
            Services.Log.Information($"Making up to \"{assignments.Count}\" item assignments");

            updatedGearpieces.AddRange(makeAssignments(assignments, gearpiecesToAssign, itemData));

            return updatedGearpieces.Distinct().ToList();
        }

        private static List<Gearpiece> makeAssignments(List<Assignment> assignments, List<Gearpiece> gearpiecesToAssign, ItemDataService itemData)
        {
            List<Gearpiece> updatedGearpieces = [];
            foreach (var assignment in assignments)
            {
                var assignableGearpieces = assignment
                    .Gearpieces
                    .Where(gearpiecesToAssign.Contains);

                foreach (var gearpiece in assignableGearpieces)
                {
                    // gearpiece unassigned
                    if (assignment.ItemId == null)
                    {
                        if (gearpiece.IsCollected)
                            updatedGearpieces.Add(gearpiece);

                        gearpiece.SetCollected(false, false);
                        continue;
                    }

                    // gearpiece assigned
                    if (assignment.ItemId == gearpiece.ItemId)
                    {
                        if (!gearpiece.IsCollected)
                            updatedGearpieces.Add(gearpiece);

                        gearpiece.MeldMultipleMateria(assignment.MateriaList);
                        gearpiece.SetCollected(true, false);
                        continue;
                    }
                }
            }

            return updatedGearpieces
                .Distinct()
                .ToList();
        }
    }
}
