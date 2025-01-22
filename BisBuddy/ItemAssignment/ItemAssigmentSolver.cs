using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using LinearAssignment;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    internal class ItemAssigmentSolver
    {
        // NOT no edge, because want to ensure solver runs, even for "no solution" cases
        public static readonly int NoEdgeWeightValue = int.MinValue + 1;
        private readonly ItemData itemData;
        private readonly List<Gearset> gearsets;
        private readonly List<GearpieceGroup> gearpiceGroups = [];
        private readonly List<PrerequesiteGroup> prerequesiteGroups = [];
        private readonly List<GameInventoryItem> gearpieceCandidateItems;
        private List<GameInventoryItem> prerequesiteCandidateItems;
        private readonly List<Gearpiece> assignedGearpieces = [];
        private readonly int[,] gearpieceEdges;
        private int[,]? prerequesiteEdges;

        public ItemAssigmentSolver(List<Gearset> gearsets, List<GameInventoryItem> inventoryItems, ItemData itemData)
        {
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
            prerequesiteCandidateItems = inventoryItems;
        }

        internal static uint GameInventoryItemId(GameInventoryItem item)
        {
            // 'normal' item
            if (!item.IsHq) return item.ItemId;

            // hq item
            return item.ItemId + ItemData.ItemIdHqOffset;
        }

        private void logSolution(int[] assignments, int[,] edges, List<GameInventoryItem> items, List<IDemandGroup> groups)
        {
            // dont look at this
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


            // stage 2: prepare prerequesite lists after gearpiece stage
            // group prerequesite items by gearpiece
            prerequesiteGroups.AddRange(groupPrerequesites(gearsets));
            var prerequesiteItemIds = prerequesiteGroups.Select(group => group.ItemId).ToHashSet();

            prerequesiteCandidateItems = prerequesiteCandidateItems
                .Where(item => prerequesiteItemIds.Contains(GameInventoryItemId(item)))
                .ToList();


            // stage 3: assign remaining valid items as prerequesites
            generatePrerequesiteEdges();
            var prerequesiteAssignments = Solver.Solve(prerequesiteEdges, maximize: true).RowAssignment;
            addPrerequesiteAssignments(result, prerequesiteAssignments);

# if DEBUG
            Services.Log.Debug("Item Assignment Solver Solution");
            Services.Log.Debug("Gearpiece Assignments");
            logSolution(gearpieceAssignments, gearpieceEdges, gearpieceCandidateItems, gearpiceGroups.Select(g => (IDemandGroup)g).ToList());
            Services.Log.Debug("Prerequesite Assignments");
            logSolution(prerequesiteAssignments, prerequesiteEdges ?? new int[0, 0], prerequesiteCandidateItems, prerequesiteGroups.Select(g => (IDemandGroup)g).ToList());
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
                    var edgeWeight = group.CandidateEdgeWeight(candidate);
                    gearpieceEdges[groupIdx, candIdx] = edgeWeight;
                }
            }
        }

        private void generatePrerequesiteEdges()
        {
            prerequesiteEdges = new int[prerequesiteGroups.Count, prerequesiteCandidateItems.Count];
            // Items -> prerequesite groups
            // prerequesite groups row
            for (var groupIdx = 0; groupIdx < prerequesiteGroups.Count; groupIdx++)
            {
                var group = prerequesiteGroups[groupIdx];

                // col
                for (var candIdx = 0; candIdx < prerequesiteCandidateItems.Count; candIdx++)
                {
                    var candidate = prerequesiteCandidateItems[candIdx];

                    // item ids match, set edge to weight score calculated by group for this candidate
                    var edgeWeight = group.CandidateEdgeWeight(candidate);
                    prerequesiteEdges[groupIdx, candIdx] = edgeWeight;
                }
            }
        }

        private List<GearpieceGroup> groupGearpieces(List<Gearset> gearsets)
        {
            var gearpieceGroups = new List<GearpieceGroup>();
            var overallGearpieceIdx = -1;

            foreach (var gearset in gearsets)
            {
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    overallGearpieceIdx++;
                    // try to add gearpiece to existing group
                    if (gearpieceGroups.Any(group => group.AddMatchingGearpiece(gearpiece, gearset))) continue;

                    // if no group was found, create a new one and add this gearpiece
                    gearpieceGroups.Add(new GearpieceGroup(gearpiece, gearset, overallGearpieceIdx, itemData));
                }
            }

            return gearpieceGroups;
        }

        private List<PrerequesiteGroup> groupPrerequesites(List<Gearset> gearsets)
        {
            var prerequesiteGroups = new List<PrerequesiteGroup>();
            var overallPrereqIdx = -1;

            foreach (var gearset in gearsets)
            {
                for (var gearpieceIdx = 0; gearpieceIdx < gearset.Gearpieces.Count; gearpieceIdx++)
                {
                    var gearpiece = gearset.Gearpieces[gearpieceIdx];

                    // already assigned in gearpiece assignment solution, don't add this to any group
                    if (assignedGearpieces.Contains(gearpiece)) continue;

                    foreach (var prerequesite in gearpiece.PrerequisiteItems)
                    {
                        overallPrereqIdx++;
                        // try to add prerequesite to existing group
                        if (prerequesiteGroups.Any(group => group.AddMatchingPrerequesite(prerequesite, gearpiece, gearset))) continue;

                        // if no group was found, create a new one and add this prerequesite
                        prerequesiteGroups.Add(new PrerequesiteGroup(prerequesite, gearpiece.ItemMateria, overallPrereqIdx, gearpiece, gearset));
                    }

                }
            }

            return prerequesiteGroups;
        }

        private void addGearpieceAssignments(SolveResult result, int[] gearpieceAssignments)
        {
            ArgumentNullException.ThrowIfNull(gearpieceAssignments);
            if (gearpieceAssignments.Length != gearpieceCandidateItems.Count)
                throw new ArgumentException("Assignment count must match candidate items count");

            var unassignedGroupIndexes = Enumerable.Range(0, gearpiceGroups.Count).ToList();

            for (var i = 0; i < gearpieceAssignments.Length; i++)
            {
                var assignment = gearpieceAssignments[i];

                // "not" assigned, skip this assignment
                if (assignment == -1 || gearpieceEdges[assignment, i] == NoEdgeWeightValue) continue;

                var candidate = gearpieceCandidateItems[i];

                if (assignment >= gearpiceGroups.Count) // invalid group (somehow)
                    throw new ArgumentException($"Invalid assignment {assignment} for candidate item {candidate.ItemId}");

                if (assignment == -1) continue; // not assigned to any gearpiece groups, move on

                // remove from unassigned, it was assigned here
                unassignedGroupIndexes.Remove(assignment);

                // add assignment to result
                result.AddAssignment(candidate, gearpiceGroups[assignment]);

                // add gearpieces to list of assigned ones, for prerequesite filtering
                assignedGearpieces.AddRange(gearpiceGroups[assignment].Gearpieces);

                // remove items assigned from list of prereq candidate items
                prerequesiteCandidateItems.Remove(candidate);
            }

            // assignments done, now add null assignements
            foreach (var unassignedIndex in unassignedGroupIndexes)
            {
                result.AddAssignment(null, gearpiceGroups[unassignedIndex]);
            }
        }

        private void addPrerequesiteAssignments(SolveResult result, int[] prerequesiteAssignments)
        {
            ArgumentNullException.ThrowIfNull(prerequesiteAssignments);
            if (prerequesiteAssignments.Length != prerequesiteCandidateItems.Count)
                throw new ArgumentException("Assignment count must match candidate items count");

            var unassignedGroupIndexes = Enumerable.Range(0, prerequesiteGroups.Count).ToList();

            for (var i = 0; i < prerequesiteAssignments.Length; i++)
            {
                var assignment = prerequesiteAssignments[i];

                // "not" assigned, skip this assignment
                if (assignment == -1 || prerequesiteEdges![assignment, i] == NoEdgeWeightValue) continue;

                var candidate = prerequesiteCandidateItems[i];

                if (assignment >= prerequesiteGroups.Count) // invalid group (somehow)
                    throw new ArgumentException($"Invalid assignment {assignment} for candidate item {candidate.ItemId}");

                if (assignment == -1) continue; // not assigned to any prereq groups, move on

                // remove from unassigned, it was assigned here
                unassignedGroupIndexes.Remove(assignment);

                // add assignment to result
                result.AddAssignment(candidate, prerequesiteGroups[assignment]);
            }

            // assignments done, now add null assignements
            foreach (var unassignedIndex in unassignedGroupIndexes)
            {
                result.AddAssignment(null, prerequesiteGroups[unassignedIndex]);
            }
        }
    }
}
