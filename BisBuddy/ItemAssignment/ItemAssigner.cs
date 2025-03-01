using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public static class ItemAssigner
    {
        // logic for applying assignments decided by solver

        public static List<Gearpiece> makeItemAssignments(SolveResult result, List<Gearpiece> gearpiecesToAssign, ItemData itemData)
        {
            var updatedGearpieces = new List<Gearpiece>();
            // Item -> Gearpiece assignments
            Services.Log.Verbose($"Assigning up to {result.GearpiecesAssignments.Count} gearpieces");
            updatedGearpieces.AddRange(assignGearpieces(result.GearpiecesAssignments, gearpiecesToAssign, itemData));

            Services.Log.Verbose($"Assigning up to {result.PrerequisitesAssignments.Count} prerequisites");
            // Item -> Prerequisite assignments
            updatedGearpieces.AddRange(assignPrerequisites(result.PrerequisitesAssignments, gearpiecesToAssign));

            return updatedGearpieces.Distinct().ToList();
        }

        private static List<Gearpiece> assignGearpieces(List<GearpiecesAssignment> assignments, List<Gearpiece> gearpiecesToAssign, ItemData itemData)
        {
            var updatedGearpieces = new List<Gearpiece>();
            foreach (var assignment in assignments)
            {
                foreach (var gearpiece in assignment.Gearpieces)
                {
                    if (gearpiecesToAssign.Contains(gearpiece))
                    {
                        if (assignment.Item != null)
                        {
                            // item assigned, collect and set materia
                            var gearpieceCollected = false;
                            if (!gearpiece.IsCollected)
                            {
                                gearpiece.SetCollected(true, false);
                                gearpieceCollected = true;
                            }
                            var materiaCountSlotted = gearpiece.MeldMultipleMateria(itemData.GetItemMateriaIds(assignment.Item.Value));

                            // toggled to collected, or new materia added
                            if (gearpieceCollected || materiaCountSlotted > 0) updatedGearpieces.Add(gearpiece);
                        }
                        else
                        {
                            // manually collected, dont touch this piece
                            if (gearpiece.IsManuallyCollected) continue;

                            var gearpieceUncollected = false;
                            if (gearpiece.IsCollected)
                            {
                                // item unassigned, uncollect
                                gearpiece.SetCollected(false, false);
                                gearpieceUncollected = true;
                            }

                            var materiaToUnmeld = gearpiece.ItemMateria.Count(m => m.IsMelded);
                            gearpiece.ItemMateria.ForEach(m => m.IsMelded = false);
                            gearpiece.ItemMateriaGrouped = null;

                            // toggled to uncollected, OR unmelded previously-melded materia
                            if (materiaToUnmeld > 0 || gearpieceUncollected) updatedGearpieces.Add(gearpiece);
                        }
                    }
                }
            }
            return updatedGearpieces;
        }

        private static List<Gearpiece> assignPrerequisites(List<PrerequisitesAssignment> assignments, List<Gearpiece> gearpiecesToAssign)
        {
            var updatedGearpieces = new List<Gearpiece>();
            foreach (var assignment in assignments)
            {
                foreach (var assignableGearpiece in gearpiecesToAssign)
                {
                    // doesn't have any prerequisites to potentially assign, skip this one
                    if (assignableGearpiece.PrerequisiteTree == null)
                        continue;

                    var gearpiecePrereqUpdateCount = updateGearpiecePrerequisites(assignableGearpiece.PrerequisiteTree, assignment);
                    // only add "one", for the whole gearpiece having at least one change
                    if (gearpiecePrereqUpdateCount > 0) updatedGearpieces.Add(assignableGearpiece);
                }
            }
            return updatedGearpieces;
        }

        private static int updateGearpiecePrerequisites(PrerequisiteNode gearpiecePrereqs, PrerequisitesAssignment assignment)
        {
            var updateCount = 0;
            if (gearpiecePrereqs == assignment.PrerequisiteGroup)
            {
                if (assignment.Item == null)
                {
                    // already uncollected, or force collected
                    if (!gearpiecePrereqs.IsCollected || gearpiecePrereqs.IsManuallyCollected) return updateCount;
                    // unassign
                    gearpiecePrereqs.SetCollected(false, false);
                    updateCount++;
                    return updateCount;
                }
                else
                {
                    // already collected, ignore
                    if (gearpiecePrereqs.IsCollected) return updateCount;
                    // colllect
                    updateCount += (1 + gearpiecePrereqs.PrerequisiteTree.Count);
                    gearpiecePrereqs.SetCollected(true, false);
                    return updateCount;
                }
            }
            else
            {
                // recurse through children
                updateCount += gearpiecePrereqs.PrerequisiteTree
                    .Sum(p => updateGearpiecePrerequisites(p, assignment));
            }
            return updateCount;
        }
    }
}
