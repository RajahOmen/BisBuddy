using BisBuddy.Gear;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    internal static class ItemAssigner
    {
        // logic for applying assignments decided by solver

        internal static List<Gearpiece> makeItemAssignments(SolveResult result, List<Gearpiece> gearpiecesToAssign, ItemData itemData)
        {
            var updatedGearpieces = new List<Gearpiece>();
            // Item -> Gearpiece assignments
            Services.Log.Verbose($"Assigning up to {result.GearpiecesAssignments.Count} gearpieces");
            updatedGearpieces.AddRange(assignGearpieces(result.GearpiecesAssignments, gearpiecesToAssign, itemData));

            Services.Log.Verbose($"Assigning up to {result.PrerequesitesAssignments.Count} prerequesites");
            // Item -> Prerequesite assignments
            updatedGearpieces.AddRange(assignPrerequesites(result.PrerequesitesAssignments, gearpiecesToAssign));

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

                            // toggled to uncollected, OR unmelded previously-melded materia
                            if (materiaToUnmeld > 0 || gearpieceUncollected) updatedGearpieces.Add(gearpiece);
                        }
                    }
                }
            }
            return updatedGearpieces;
        }

        private static List<Gearpiece> assignPrerequesites(List<PrerequesitesAssignment> assignments, List<Gearpiece> gearpiecesToAssign)
        {
            var updatedGearpieces = new List<Gearpiece>();
            foreach (var assignment in assignments)
            {
                foreach (var assignedGearpiece in gearpiecesToAssign)
                {
                    var gearpiecePrereqUpdateCount = updateGearpiecePrerequesites(assignedGearpiece.PrerequisiteItems, assignment);
                    // only add "one", for the whole gearpiece having at least one change
                    if (gearpiecePrereqUpdateCount > 0) updatedGearpieces.Add(assignedGearpiece);
                }
            }
            return updatedGearpieces;
        }

        private static int updateGearpiecePrerequesites(List<GearpiecePrerequesite> gearpiecePrereqs, PrerequesitesAssignment assignment)
        {
            var updateCount = 0;
            foreach (var prereq in gearpiecePrereqs)
            {
                if (prereq == assignment.GearpiecePrerequesite)
                {
                    if (assignment.Item == null)
                    {
                        // already uncollected, or force collected
                        if (!prereq.IsCollected || prereq.IsManuallyCollected) continue;
                        // unassign
                        prereq.SetCollected(false, false);
                        updateCount++;
                        return updateCount;
                    }
                    else
                    {
                        // already collected, ignore
                        if (prereq.IsCollected) continue;
                        // colllect
                        updateCount += (1 + prereq.Prerequesites.Count);
                        prereq.SetCollected(true, false);
                        return updateCount;
                    }
                }
                else
                {
                    // recurse through children
                    updateCount += updateGearpiecePrerequesites(prereq.Prerequesites, assignment);
                }
            }
            return updateCount;
        }
    }
}
