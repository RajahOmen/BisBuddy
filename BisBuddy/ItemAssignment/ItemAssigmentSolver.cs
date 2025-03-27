using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using LinearAssignment;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class ItemAssigmentSolver
    {
        // NOT no edge, because want to ensure solver runs, even for "no solution" cases
        public static readonly int NoEdgeWeightValue = int.MinValue + 1;
        // for edges between for dummy group assignments and their items
        public static readonly int DummyEdgeWeightValue = NoEdgeWeightValue + 1;

        private readonly bool strictMateriaMatching;
        private readonly ItemData itemData;
        private readonly List<Gearset> allGearsets;
        private readonly List<Gearset> assignableGearsets;
        private readonly List<GearpieceAssignmentGroup> gearpieceGroups = [];
        private readonly List<PrerequisiteAssignmentGroup> prerequisiteGroups = [];
        private readonly List<GameInventoryItem> gearpieceCandidateItems;
        private List<GameInventoryItem> prerequisiteCandidateItems;
        private readonly List<Assignment> assignments = [];
        private readonly List<Gearpiece> assignedGearpieces = [];
        private readonly int[,] gearpieceEdges;
        private int[,]? prerequisiteEdges;

        public ItemAssigmentSolver(
            List<Gearset> allGearsets,
            List<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems,
            ItemData itemData,
            bool strictMateriaMatching
            )
        {
            this.strictMateriaMatching = strictMateriaMatching;
            this.itemData = itemData;
            this.allGearsets = allGearsets;
            this.assignableGearsets = assignableGearsets;

            // group gearpieces by id/materia
            gearpieceGroups.AddRange(groupGearpieces(allGearsets));
            var gearpieceItemIds = gearpieceGroups.Select(group => group.ItemId).ToHashSet();

            removeManuallyCollectedItems(allGearsets, inventoryItems);

            // filter out unused candidate items
            gearpieceCandidateItems = inventoryItems
                .Where(item => gearpieceGroups.Any(g => g.NeedsItemId(ItemData.GetGameInventoryItemId(item))))
                .ToList();

            // add dummy groups to fill out groups to ensure every candidate has one group to be assigned to
            var groupItems = gearpieceGroups.Select(g => g.ItemId).ToList();
            var extraCandidateItems = gearpieceCandidateItems.Select(ItemData.GetGameInventoryItemId).ToList();
            groupItems.ForEach(groupItemId => extraCandidateItems.Remove(groupItemId));
            gearpieceGroups.AddRange(extraCandidateItems.Select(itemId => new GearpieceAssignmentGroup(itemId)));

            gearpieceEdges = new int[gearpieceGroups.Count, gearpieceCandidateItems.Count];

            // set to all items, filter after gearpiece assignment
            prerequisiteCandidateItems = inventoryItems;
        }

        private static void removeManuallyCollectedItems(List<Gearset> gearsets, List<GameInventoryItem> inventoryItems)
        {
            var manuallyCollectedItemIds = gearsets
                .SelectMany(set =>
                    set.Gearpieces.SelectMany(gear =>
                        gear.ManuallyCollectedItemIds()
                ));

            // try removing copies of manually collected items from pool of avaliable items to assign
            foreach (var itemId in manuallyCollectedItemIds)
            {
                for (var invItemIdx = inventoryItems.Count - 1; invItemIdx >= 0; invItemIdx--)
                {
                    var invItem = inventoryItems[invItemIdx];
                    if (ItemData.GetGameInventoryItemId(invItem) == itemId)
                    {
                        inventoryItems.RemoveAt(invItemIdx);
                        break;
                    }
                }
            }
        }

        private void logSolution(int[] assignments, int[,] edges, List<GameInventoryItem> items, List<IAssignmentGroup> groups)
        {
            var rows = edges.GetLength(0);
            var columns = edges.GetLength(1);

            var labelName = "Candidate Inventory Items ->  ".PadLeft(60);
            var itemNames = items
                .Select(
                    c =>
                        itemData.GetItemNameById(c.ItemId).Length > 12
                        ? $"{itemData.GetItemNameById(c.ItemId)[..6]}-{itemData.GetItemNameById(c.ItemId)[^5..]}"
                        : $"{itemData.GetItemNameById(c.ItemId),12}"
                    );
            var candidateItemIdLabel = labelName + string.Join(" ", itemNames);
            Services.Log.Debug(candidateItemIdLabel);

            for (var row = 0; row < rows; row++)
            {
                var assIdx = -1;
                for (var i = 0; i < assignments.Length; i++)
                {
                    if (assignments[i] == row)
                    {
                        assIdx = i;
                        break;
                    }
                }

                // Convert the entire row into a formatted string
                var itemName = itemData.GetItemNameById(groups[row].ItemId).Replace($"{ItemData.HqIcon}", "[HQ]");
                var itemIdx = $"[{row,2}]";
                var itemAssignment =
                    (assIdx != -1 && edges[row, assIdx] != NoEdgeWeightValue)
                    ? "[*]"
                    : "[ ]";
                var itemEdges = string.Join(" ",
                    Enumerable.Range(0, columns)
                    .Select(col =>
                        $"{$"{(
                            edges[row, col] == NoEdgeWeightValue
                            ? (col == assIdx ? "------ [*]" : "----------")
                            : edges[row, col] == DummyEdgeWeightValue
                            ? (col == assIdx ? "-DUMMY [*]" : "--DUMMY---")
                            : $"{edges[row, col]}{(col == assIdx ? " [*]" : "")}"
                        )}",12}"
                    ));
                var line = $"{itemName} {itemIdx} {itemAssignment}".PadLeft(60) + itemEdges;
                Services.Log.Debug(line);
            }
        }

        public List<Assignment> Solve()
        {
            // stage 1: assign gearpieces
            generateGearpieceEdges();
            int[] gearpieceAssignments;
            try
            {
                gearpieceAssignments = Solver.Solve(gearpieceEdges, maximize: true).RowAssignment;
            }
            catch (InvalidOperationException)
            {
                logSolution(
                    [],
                    gearpieceEdges,
                    gearpieceCandidateItems,
                    gearpieceGroups
                        .Select(g => (IAssignmentGroup)g)
                        .ToList()
                        );
                throw;
            }
            // this removes candidate items from prereq list as well
            addGearpieceAssignments(gearpieceAssignments);

            // stage 2: prepare prerequisite lists after gearpiece stage
            // group prerequisite items by gearpiece
            prerequisiteGroups.AddRange(groupPrerequisites(allGearsets));
            // filter item list to only needed items
            prerequisiteCandidateItems = prerequisiteCandidateItems
                .Where(item => prerequisiteGroups.Any(g => g.NeedsItemId(ItemData.GetGameInventoryItemId(item))))
                .ToList();

            // stage 3: assign remaining valid items as prerequisites
            var prerequisiteAssignments = solveAndAssignPrerequisites();
#if DEBUG
            Services.Log.Debug("Item Assignment Solver Solution");

            Services.Log.Debug("Gearpiece Assignments");
            logSolution(
                gearpieceAssignments,
                gearpieceEdges,
                gearpieceCandidateItems,
                gearpieceGroups
                    .Select(g => (IAssignmentGroup)g)
                    .ToList()
                    );

            Services.Log.Debug("Prerequisite Assignments");
            logSolution(
                prerequisiteAssignments,
                prerequisiteEdges ?? new int[0, 0],
                prerequisiteCandidateItems,
                prerequisiteGroups
                    .Select(g => (IAssignmentGroup)g)
                    .ToList()
                );
#endif
            return assignments;
        }

        private void unassignPrereqs()
        {
            var gearpieces = Gearset.GetGearpiecesFromGearsets(assignableGearsets);
            gearpieces.ForEach(g => g.PrerequisiteTree?.SetCollected(false, false));
        }

        private int[] solveAndAssignPrerequisites()
        {
            // assignments: index = candidate item index
            //              value = prerequisite group index

            // unassign all prereqs for assignable gearpieces in advance of assigning
            unassignPrereqs();

            prerequisiteEdges = new int[prerequisiteGroups.Count, prerequisiteCandidateItems.Count];
            var assignments = new int[prerequisiteCandidateItems.Count];

            var candidateItemList = new List<GameInventoryItem>(prerequisiteCandidateItems);

            var itemCount = candidateItemList.Count;
            // n(n+1)/2 + buffer
            var maxLoops = (itemCount * (itemCount + 1) / 2) + 200;
            var loopCount = 0;
            do
            {
                // no more items to assign
                if (candidateItemList.Count == 0)
                    break;

                // pop first item id to be assigned in queue
                var itemToAssign = candidateItemList[0];
                var itemIdToAssign = ItemData.GetGameInventoryItemId(candidateItemList[0]);
                candidateItemList.RemoveAt(0);
                var itemIdx = prerequisiteCandidateItems.IndexOf(itemToAssign);

                // find prereq group with highest edge score for this item
                var bestAssignScore = NoEdgeWeightValue;
                PrerequisiteAssignmentGroup? bestGroup = null;
                var bestGroupIdx = -1;
                for (var groupIdx = 0; groupIdx < prerequisiteGroups.Count; groupIdx++)
                {
                    var prereqGroup = prerequisiteGroups[groupIdx];
                    var assignScore = prereqGroup.CandidateEdgeWeight(itemIdToAssign, []);

                    if (loopCount < itemCount)
                        prerequisiteEdges[groupIdx, loopCount] = assignScore;

                    if (assignScore > bestAssignScore)
                    {
                        bestAssignScore = assignScore;
                        bestGroup = prereqGroup;
                        bestGroupIdx = groupIdx;
                    }
                }

                // group was found
                if (bestGroup != null)
                {
                    assignments[itemIdx] = bestGroupIdx;

                    // assign item. If assignment would shadow earlier-assigned items, re-add to assignment queue
                    var oldAssignedItems = bestGroup.AssignItem(itemToAssign);
                    candidateItemList.AddRange(oldAssignedItems);
                }
            }
            while (loopCount++ < maxLoops);

            if (loopCount >= maxLoops)
                Services.Log.Warning($"Max prerequisite solve loop count reached with \"{prerequisiteCandidateItems.Count}\" items unprocessed");

            return assignments;
        }

        private void generateGearpieceEdges()
        {
            // Items -> gearpiece groups
            // gearpiece groups row
            for (var groupIdx = 0; groupIdx < gearpieceGroups.Count; groupIdx++)
            {
                var group = gearpieceGroups[groupIdx];

                // col
                for (var candIdx = 0; candIdx < gearpieceCandidateItems.Count; candIdx++)
                {
                    var candidate = gearpieceCandidateItems[candIdx];

                    // item ids match, set edge to weight score calculated by group for this candidate
                    var candidateId = ItemData.GetGameInventoryItemId(candidate);
                    var candidateMateria = itemData.GetItemMateria(candidate);
                    var edgeWeight = group.CandidateEdgeWeight(candidateId, candidateMateria);
                    gearpieceEdges[groupIdx, candIdx] = edgeWeight;
                }
            }
        }

        private List<GearpieceAssignmentGroup> groupGearpieces(List<Gearset> gearsets)
        {
            var gearpieceGroups = new List<GearpieceAssignmentGroup>();
            var overallGearpieceIdx = -1;

            foreach (var gearset in gearsets)
            {
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // do not consider manually collected items in solver
                    if (gearpiece.IsManuallyCollected)
                        continue;

                    overallGearpieceIdx++;
                    // try to add gearpiece to existing group
                    if (gearpieceGroups.Any(group => group.AddMatchingGearpiece(gearpiece, gearset))) continue;

                    // if no group was found, create a new one and add this gearpiece
                    gearpieceGroups.Add(new GearpieceAssignmentGroup(gearpiece, gearset, overallGearpieceIdx));
                }
            }

            return gearpieceGroups;
        }

        private List<PrerequisiteAssignmentGroup> groupPrerequisites(List<Gearset> gearsets)
        {
            var prerequisiteGroups = new List<PrerequisiteAssignmentGroup>();
            var overallPrereqIdx = -1;

            foreach (var gearset in gearsets)
            {
                for (var gearpieceIdx = 0; gearpieceIdx < gearset.Gearpieces.Count; gearpieceIdx++)
                {
                    var gearpiece = gearset.Gearpieces[gearpieceIdx];

                    // do not consider manually collected items in solver
                    if (gearpiece.IsManuallyCollected)
                        continue;

                    // has no prerequisites to potentially assign
                    if (gearpiece.PrerequisiteTree == null)
                        continue;

                    // already assigned in gearpiece assignment solution or is manually collected, don't add this to any group
                    if (assignedGearpieces.Contains(gearpiece) || gearpiece.IsManuallyCollected)
                        continue;

                    overallPrereqIdx++;

                    // try to add prerequisite to existing group
                    if (prerequisiteGroups.Any(group => group.AddMatchingGearpiece(gearpiece, gearset)))
                        continue;

                    // if no group was found, create a new one and add this prerequisite
                    prerequisiteGroups.Add(new PrerequisiteAssignmentGroup(
                        gearpiece,
                        gearset,
                        overallPrereqIdx,
                        strictMateriaMatching
                        ));
                }
            }

            return prerequisiteGroups;
        }

        private void addGearpieceAssignments(int[] gearpieceAssignments)
        {
            ArgumentNullException.ThrowIfNull(gearpieceAssignments);
            if (gearpieceAssignments.Length != gearpieceCandidateItems.Count)
                throw new ArgumentException("Assignment count must match candidate items count");

            var unassignedGroupIndexes = Enumerable
                .Range(0, gearpieceGroups.Count)
                .Where(i => !gearpieceAssignments.Contains(i))
                .ToList();

            if (!strictMateriaMatching)
            {
                addUnassignedGearpieceGroups(unassignedGroupIndexes, gearpieceAssignments);
            }

            for (var i = 0; i < gearpieceAssignments.Length; i++)
            {
                var assignment = gearpieceAssignments[i];

                // "not" assigned, skip this assignment
                if (assignment == -1 || gearpieceEdges[assignment, i] == NoEdgeWeightValue) continue;

                var candidate = gearpieceCandidateItems[i];

                if (assignment >= gearpieceGroups.Count) // invalid group (somehow)
                    throw new ArgumentException($"Invalid assignment {assignment} for candidate item {candidate.ItemId}");

                // add assignment to assignments list
                var groupToAssign = gearpieceGroups[assignment];
                assignments.Add(
                    new Assignment
                    {
                        ItemId = ItemData.GetGameInventoryItemId(candidate),
                        MateriaList = itemData.GetItemMateria(candidate),
                        Gearpieces = groupToAssign.Gearpieces
                    });

                // add gearpieces to list of assigned ones, for prerequisite filtering
                assignedGearpieces.AddRange(gearpieceGroups[assignment].Gearpieces);

                // remove items assigned from list of prereq candidate items
                prerequisiteCandidateItems.Remove(candidate);
            }

            // assignments done, now add null assignements
            foreach (var unassignedIndex in unassignedGroupIndexes)
            {
                var groupToAssign = gearpieceGroups[unassignedIndex];
                assignments.Add(
                    new Assignment
                    {
                        ItemId = null,
                        Gearpieces = groupToAssign.Gearpieces
                    });
            }
        }

        private void addUnassignedGearpieceGroups(List<int> unassignedGroupIndexes, int[] gearpieceAssignments)
        {
            var unassignedGroupsCount = unassignedGroupIndexes.Count;
            for (var i = unassignedGroupsCount - 1; i >= 0; i--)
            {
                var unassignedGroup = gearpieceGroups[unassignedGroupIndexes[i]];
                var validAssignedGroups = gearpieceAssignments
                    .Select(assignIdx => gearpieceGroups[assignIdx])
                    .Where(g => g.ItemId == unassignedGroup.ItemId)
                    .OrderByDescending(g => unassignedGroup.CandidateEdgeWeight(g.ItemId, g.MateriaList));

                // get gearset-gearpiece pairing
                var unassignedGearpieceGearsets = unassignedGroup
                    .Gearpieces
                    .Select(piece => (piece, unassignedGroup.Gearsets.First(set => set.Gearpieces.Contains(piece))))
                    .ToDictionary();

                foreach (var (gearpiece, gearset) in unassignedGearpieceGearsets)
                {
                    foreach (var validAssignedGroup in validAssignedGroups)
                    {
                        // can't assign to this group since it already has a piece from this gearset in it
                        if (validAssignedGroup.Gearsets.Contains(gearset))
                            continue;

                        validAssignedGroup.Gearpieces.Add(gearpiece);
                        validAssignedGroup.Gearsets.Add(gearset);
                        unassignedGroup.Gearpieces.Remove(gearpiece);
                        break;
                    }
                }

                if (unassignedGroup.Gearpieces.Count == 0)
                {
                    unassignedGroupIndexes.RemoveAt(i);
                }
            }
        }

        //private void assignUnassignedPrerequisiteGroups(List<int> unassignedGroupIndexes, int[] prerequisiteAssignments)
        //{
        //    var unassignedGroupsCount = unassignedGroupIndexes.Count;
        //    for (var i = unassignedGroupsCount - 1; i >= 0; i--)
        //    {
        //        var unassignedGroup = prerequisiteGroups[unassignedGroupIndexes[i]];
        //        Services.Log.Verbose($"unassigned group item id: {unassignedGroup.ItemId}. Assigned item ids: {string.Join(", ", prerequisiteAssignments.Select(i => prerequisiteGroups[i].ItemId))}");

        //        var validAssignedGroups = prerequisiteAssignments
        //            .Select(assignIdx => prerequisiteGroups[assignIdx])
        //            .Where(g => g.ItemId == unassignedGroup.ItemId);
        //        //.OrderByDescending(g =>
        //        //    g.DirectlyAssignedNodes
        //        //        .SelectMany(assignments =>
        //        //            assignments.Value
        //        //                .Select(assignment =>
        //        //                    unassignedGroup.CandidateEdgeWeight(assignment.Node.ItemId, []))
        //        //                )
        //        //        .Max()
        //        //    );

        //        return;

        //        Services.Log.Verbose($"Valid assigned groups: {validAssignedGroups.Count()}");

        //        // get gearset-prerequisite pairing
        //        var unassignedPrereqGearsets = unassignedGroup
        //            .Gearpieces
        //            .Select(prereq => (prereq, unassignedGroup.Gearsets.First(set => set.Gearpieces.Where(g => g.PrerequisiteTree == prereq).Any())))
        //            .ToDictionary();

        //        foreach (var (unassignedGearpiece, gearset) in unassignedPrereqGearsets)
        //        {
        //            foreach (var validAssignedGroup in validAssignedGroups)
        //            {
        //                // can't assign to this group since it already has a prereq from this gearset in it
        //                if (validAssignedGroup.Gearsets.Contains(gearset))
        //                    continue;

        //                Services.Log.Verbose($"{unassignedGearpiece.ItemName} valid group: {validAssignedGroup.ItemId}");

        //                validAssignedGroup.Gearpieces.Add(unassignedGearpiece);
        //                validAssignedGroup.Gearsets.Add(gearset);

        //                var assignedItems = validAssignedGroup.DirectlyAssignedNodes.SelectMany(g => g.Value.Select(a => a.Item)).Distinct();
        //                foreach (var item in assignedItems)
        //                {
        //                    unassignedGearpiece.PrerequisiteTree?.AssignItemId(ItemData.GetGameInventoryItemId(item));
        //                }

        //                unassignedGroup.Gearpieces.Remove(unassignedGearpiece);
        //                break;
        //            }
        //        }

        //        if (unassignedGroup.Gearpieces.Count == 0)
        //        {
        //            unassignedGroupIndexes.RemoveAt(i);
        //        }
        //    }
        //}
    }
}
