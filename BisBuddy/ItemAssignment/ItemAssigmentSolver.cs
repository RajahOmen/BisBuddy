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

        private readonly bool strictMateriaMatching;
        private readonly ItemData itemData;
        private readonly List<Gearset> gearsets;
        private readonly List<GearpieceAssignmentGroup> gearpiceGroups = [];
        private readonly List<PrerequisiteAssignmentGroup> prerequisiteGroups = [];
        private readonly List<GameInventoryItem> gearpieceCandidateItems;
        private List<GameInventoryItem> prerequisiteCandidateItems;
        private readonly List<Gearpiece> assignedGearpieces = [];
        private readonly int[,] gearpieceEdges;
        private int[,]? prerequisiteEdges;

        public ItemAssigmentSolver(
            List<Gearset> gearsets,
            List<GameInventoryItem> inventoryItems,
            ItemData itemData,
            bool strictMateriaMatching
            )
        {
            this.strictMateriaMatching = strictMateriaMatching;
            this.itemData = itemData;
            this.gearsets = gearsets;

            // group gearpieces by id/materia
            gearpiceGroups.AddRange(groupGearpieces(gearsets));
            var gearpieceItemIds = gearpiceGroups.Select(group => group.ItemId).ToHashSet();

            gearpieceCandidateItems = inventoryItems
                .Where(item => gearpieceItemIds.Contains(GameInventoryItemId(item)))
                .ToList();

            gearpieceEdges = new int[gearpiceGroups.Count, gearpieceCandidateItems.Count];

            // set to all items, filter after gearpiece assignment
            prerequisiteCandidateItems = inventoryItems;
        }

        public static uint GameInventoryItemId(GameInventoryItem item)
        {
            // 'normal' item
            if (!item.IsHq) return item.ItemId;

            // hq item
            return item.ItemId + ItemData.ItemIdHqOffset;
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
                            : $"{edges[row, col]}{(col == assIdx ? " [*]" : "")}"
                        )}",12}"
                    ));
                var line = $"{itemName} {itemIdx} {itemAssignment}".PadLeft(60) + itemEdges;
                Services.Log.Debug(line);
            }
        }

        public SolveResult Solve()
        {
            var result = new SolveResult();

            // stage 1: assign gearpieces
            generateGearpieceEdges();
            var gearpieceAssignments = Solver.Solve(gearpieceEdges, maximize: true).RowAssignment;
            addGearpieceAssignments(result, gearpieceAssignments);


            // stage 2: prepare prerequisite lists after gearpiece stage
            // group prerequisite items by gearpiece
            prerequisiteGroups.AddRange(groupPrerequisites(gearsets));
            var prerequisiteItemIds = prerequisiteGroups.Select(group => group.ItemId).ToHashSet();

            prerequisiteCandidateItems = prerequisiteCandidateItems
                .Where(item => prerequisiteItemIds.Contains(GameInventoryItemId(item)))
                .ToList();


            // stage 3: assign remaining valid items as prerequisites
            generatePrerequisiteEdges();
            var prerequisiteAssignments = Solver.Solve(prerequisiteEdges, maximize: true).RowAssignment;
            addPrerequisiteAssignments(result, prerequisiteAssignments);

# if DEBUG
            Services.Log.Debug("Item Assignment Solver Solution");
            Services.Log.Debug("Gearpiece Assignments");
            logSolution(gearpieceAssignments, gearpieceEdges, gearpieceCandidateItems, gearpiceGroups.Select(g => (IAssignmentGroup)g).ToList());
            Services.Log.Debug("Prerequisite Assignments");
            logSolution(prerequisiteAssignments, prerequisiteEdges ?? new int[0, 0], prerequisiteCandidateItems, prerequisiteGroups.Select(g => (IAssignmentGroup)g).ToList());
#endif
            return result;
        }

        private void generateGearpieceEdges()
        {
            // Items -> gearpiece groups
            // gearpiece groups row
            for (var groupIdx = 0; groupIdx < gearpiceGroups.Count; groupIdx++)
            {
                var group = gearpiceGroups[groupIdx];

                // col
                for (var candIdx = 0; candIdx < gearpieceCandidateItems.Count; candIdx++)
                {
                    var candidate = gearpieceCandidateItems[candIdx];

                    // item ids match, set edge to weight score calculated by group for this candidate
                    var candidateId = ItemData.GameInventoryItemId(candidate);
                    var candidateMateria = itemData.GetItemMateria(candidate);
                    var edgeWeight = group.CandidateEdgeWeight(candidateId, candidateMateria);
                    gearpieceEdges[groupIdx, candIdx] = edgeWeight;
                }
            }
        }

        private void generatePrerequisiteEdges()
        {
            prerequisiteEdges = new int[prerequisiteGroups.Count, prerequisiteCandidateItems.Count];
            // Items -> prerequisite groups
            // prerequisite groups row
            for (var groupIdx = 0; groupIdx < prerequisiteGroups.Count; groupIdx++)
            {
                var group = prerequisiteGroups[groupIdx];

                // col
                for (var candIdx = 0; candIdx < prerequisiteCandidateItems.Count; candIdx++)
                {
                    var candidate = prerequisiteCandidateItems[candIdx];

                    // item ids match, set edge to weight score calculated by group for this candidate
                    var candidateId = ItemData.GameInventoryItemId(candidate);
                    var edgeWeight = group.CandidateEdgeWeight(candidateId, []);
                    prerequisiteEdges[groupIdx, candIdx] = edgeWeight;
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

                    // has no prerequisites to potentially assign
                    if (gearpiece.PrerequisiteTree == null)
                        continue;

                    // already assigned in gearpiece assignment solution or is manually collected, don't add this to any group
                    if (assignedGearpieces.Contains(gearpiece) || gearpiece.IsManuallyCollected)
                        continue;

                    overallPrereqIdx++;

                    // try to add prerequisite to existing group
                    if (prerequisiteGroups.Any(group => group.AddMatchingPrerequisite(gearpiece.PrerequisiteTree, gearpiece, gearset)))
                        continue;

                    // if no group was found, create a new one and add this prerequisite
                    prerequisiteGroups.Add(new PrerequisiteAssignmentGroup(
                        gearpiece.PrerequisiteTree,
                        gearpiece.ItemMateria,
                        overallPrereqIdx,
                        gearpiece,
                        gearset
                        ));
                }
            }

            return prerequisiteGroups;
        }

        private void addGearpieceAssignments(SolveResult result, int[] gearpieceAssignments)
        {
            ArgumentNullException.ThrowIfNull(gearpieceAssignments);
            if (gearpieceAssignments.Length != gearpieceCandidateItems.Count)
                throw new ArgumentException("Assignment count must match candidate items count");

            var unassignedGroupIndexes = Enumerable
                .Range(0, gearpiceGroups.Count)
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

                if (assignment >= gearpiceGroups.Count) // invalid group (somehow)
                    throw new ArgumentException($"Invalid assignment {assignment} for candidate item {candidate.ItemId}");

                // add assignment to result
                result.AddAssignment(candidate, gearpiceGroups[assignment]);

                // add gearpieces to list of assigned ones, for prerequisite filtering
                assignedGearpieces.AddRange(gearpiceGroups[assignment].Gearpieces);

                // remove items assigned from list of prereq candidate items
                prerequisiteCandidateItems.Remove(candidate);
            }

            // assignments done, now add null assignements
            foreach (var unassignedIndex in unassignedGroupIndexes)
            {
                result.AddAssignment(null, gearpiceGroups[unassignedIndex]);
            }
        }

        private void addPrerequisiteAssignments(SolveResult result, int[] prerequisiteAssignments)
        {
            ArgumentNullException.ThrowIfNull(prerequisiteAssignments);
            if (prerequisiteAssignments.Length != prerequisiteCandidateItems.Count)
                throw new ArgumentException("Assignment count must match candidate items count");

            var unassignedGroupIndexes = Enumerable
                .Range(0, prerequisiteGroups.Count)
                .Where(i => !prerequisiteAssignments.Contains(i))
                .ToList();

            if (!strictMateriaMatching)
            {
                addUnassignedPrerequisiteGroups(unassignedGroupIndexes, prerequisiteAssignments);
            }

            for (var i = 0; i < prerequisiteAssignments.Length; i++)
            {
                var assignment = prerequisiteAssignments[i];

                // "not" assigned, skip this assignment
                if (assignment == -1 || prerequisiteEdges![assignment, i] == NoEdgeWeightValue) continue;

                var candidate = prerequisiteCandidateItems[i];

                if (assignment >= prerequisiteGroups.Count) // invalid group (somehow)
                    throw new ArgumentException($"Invalid assignment {assignment} for candidate item {candidate.ItemId}");

                // add assignment to result
                result.AddAssignment(candidate, prerequisiteGroups[assignment]);
            }

            // assignments done, now add null assignements
            foreach (var unassignedIndex in unassignedGroupIndexes)
            {
                result.AddAssignment(null, prerequisiteGroups[unassignedIndex]);
            }
        }

        private void addUnassignedGearpieceGroups(List<int> unassignedGroupIndexes, int[] gearpieceAssignments)
        {
            var unassignedGroupsCount = unassignedGroupIndexes.Count;
            for (var i = unassignedGroupsCount - 1; i >= 0; i--)
            {
                var unassignedGroup = gearpiceGroups[unassignedGroupIndexes[i]];
                var validAssignedGroups = gearpieceAssignments
                    .Select(assignIdx => gearpiceGroups[assignIdx])
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

        private void addUnassignedPrerequisiteGroups(List<int> unassignedGroupIndexes, int[] prerequisiteAssignments)
        {
            var unassignedGroupsCount = unassignedGroupIndexes.Count;
            for (var i = unassignedGroupsCount - 1; i >= 0; i--)
            {
                var unassignedGroup = prerequisiteGroups[unassignedGroupIndexes[i]];
                var validAssignedGroups = prerequisiteAssignments
                    .Select(assignIdx => prerequisiteGroups[assignIdx])
                    .Where(g => g.ItemId == unassignedGroup.ItemId)
                    .OrderByDescending(g => unassignedGroup.CandidateEdgeWeight(g.ItemId, g.MateriaList));

                // get gearset-prerequisite pairing
                var unassignedPrereqGearsets = unassignedGroup
                    .PrerequisiteGroups
                    .Select(prereq => (prereq, unassignedGroup.Gearsets.First(set => set.Gearpieces.Where(g => g.PrerequisiteTree == prereq).Any())))
                    .ToDictionary();

                foreach (var (prerequisite, gearset) in unassignedPrereqGearsets)
                {
                    foreach (var validAssignedGroup in validAssignedGroups)
                    {
                        // can't assign to this group since it already has a prereq from this gearset in it
                        if (validAssignedGroup.Gearsets.Contains(gearset))
                            continue;

                        validAssignedGroup.PrerequisiteGroups.Add(prerequisite);
                        validAssignedGroup.Gearsets.Add(gearset);
                        unassignedGroup.PrerequisiteGroups.Remove(prerequisite);
                        break;
                    }
                }

                if (unassignedGroup.PrerequisiteGroups.Count == 0)
                {
                    unassignedGroupIndexes.RemoveAt(i);
                }
            }
        }
    }
}
