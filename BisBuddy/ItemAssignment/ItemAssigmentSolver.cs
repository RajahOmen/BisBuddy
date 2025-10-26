using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Util;
using Dalamud.Game.Inventory;
using LinearAssignment;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class ItemAssigmentSolver : IItemAssignmentSolver
    {
        // NOT no edge, because want to ensure solver runs, even for "no solution" cases
        public static readonly int NoEdgeWeightValue = int.MinValue + 1;
        // for edges between for dummy group assignments and their items
        public static readonly int DummyEdgeWeightValue = NoEdgeWeightValue + 1;

        public SolveResult? GearpiecesResult { get; private set; } = null;
        public SolveResult? PrerequisitesResult { get; private set; } = null;

        private readonly ITypedLogger<ItemAssigmentSolver> logger;
        private readonly bool strictMateriaMatching;
        private readonly bool assignPrerequisiteMateria;
        private readonly IItemDataService itemData;
        private readonly IMateriaFactory materiaFactory;
        private readonly IEnumerable<Gearset> allGearsets;
        private readonly IEnumerable<Gearset> assignableGearsets;
        private readonly List<GearpieceAssignmentGroup> gearpieceGroups = [];
        private readonly List<PrerequisiteAssignmentGroup> prerequisiteGroups = [];
        private readonly List<GameInventoryItem> gearpieceCandidateItems;
        private List<GameInventoryItem> prerequisiteCandidateItems;
        private readonly List<Assignment> assignments = [];
        private readonly List<Gearpiece> assignedGearpieces = [];
        private readonly int[,] gearpieceEdges;
        private int[,]? prerequisiteEdges;

        public ItemAssigmentSolver(
            ITypedLogger<ItemAssigmentSolver> logger,
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems,
            IItemDataService itemData,
            IMateriaFactory materiaFactory,
            bool strictMateriaMatching,
            bool assignPrerequisiteMateria
            )
        {
            this.logger = logger;
            this.strictMateriaMatching = strictMateriaMatching;
            this.assignPrerequisiteMateria = assignPrerequisiteMateria;
            this.itemData = itemData;
            this.materiaFactory = materiaFactory;
            this.allGearsets = allGearsets.Where(g => g.IsActive);
            this.assignableGearsets = assignableGearsets.Where(g => g.IsActive);

            // group gearpieces by id/materia
            gearpieceGroups.AddRange(groupGearpieces(this.allGearsets));
            var gearpieceItemIds = gearpieceGroups.Select(group => group.ItemId).ToHashSet();

            removeManuallyCollectedItems(this.allGearsets, inventoryItems);

            // filter out unused candidate items
            gearpieceCandidateItems = inventoryItems
                .Where(item => gearpieceGroups.Any(g => g.NeedsItemId(item.ItemId)))
                .ToList();

            // add dummy groups to fill out groups to ensure every candidate has one group to be assigned to
            var groupItems = gearpieceGroups.Select(g => g.ItemId).ToList();
            var extraCandidateItems = gearpieceCandidateItems.Select(i => i.ItemId).ToList();
            groupItems.ForEach(groupItemId => extraCandidateItems.Remove(groupItemId));
            gearpieceGroups.AddRange(extraCandidateItems.Select(itemId => new GearpieceAssignmentGroup(itemId)));

            gearpieceEdges = new int[gearpieceGroups.Count, gearpieceCandidateItems.Count];

            // set to all items, filter after gearpiece assignment
            prerequisiteCandidateItems = inventoryItems;
        }

        private static void removeManuallyCollectedItems(IEnumerable<Gearset> gearsets, List<GameInventoryItem> inventoryItems)
        {
            var manuallyCollectedItemIds = gearsets
                .SelectMany(set =>
                    set.Gearpieces.SelectMany(gear =>
                        gear.CollectLockItemIds()
                ));

            // try removing copies of manually collected items from pool of avaliable items to assign
            foreach (var itemId in manuallyCollectedItemIds)
            {
                for (var invItemIdx = inventoryItems.Count - 1; invItemIdx >= 0; invItemIdx--)
                {
                    var invItem = inventoryItems[invItemIdx];
                    if (invItem.ItemId == itemId)
                    {
                        inventoryItems.RemoveAt(invItemIdx);
                        break;
                    }
                }
            }
        }

        private void logResult(bool logPrereqs = false)
        {
            SolveResult? resultToLog;
            if (logPrereqs)
                resultToLog = PrerequisitesResult;
            else
                resultToLog = GearpiecesResult;

            if (resultToLog is not SolveResult result)
            {
                logger.Warning($"Tried to log solve, got null result");
                return;
            }

            var (assignments, edges, items, groups) = result;

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
            logger.Debug(candidateItemIdLabel);

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
                var itemName = itemData.GetItemNameById(groups[row].ItemId).Replace($"{Constants.HqIcon}", "[HQ]");
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
                logger.Debug(line);
            }
        }

        public List<Gearpiece> SolveAndAssign()
        {
            // stage 1: solve gearpiece assignments
            generateGearpieceEdges();
            int[] gearpieceAssignments;
            try
            {
                logger.Verbose($"Solving with {gearpieceEdges.Length} edges...");
                gearpieceAssignments = Solver.Solve(gearpieceEdges, maximize: true).RowAssignment;
                logger.Verbose($"Solved gearpiece assignments with {gearpieceAssignments.Length} assignments");
            }
            catch (InvalidOperationException)
            {
                GearpiecesResult = new()
                {
                    Assignments = [],
                    Edges = gearpieceEdges,
                    CandidateItems = gearpieceCandidateItems,
                    AssignmentGroups = gearpieceGroups
                        .Select(g => (IAssignmentGroup)g)
                        .ToList()
                };
                logResult();
                throw;
            }
            // this removes candidate items from prereq list as well
            addGearpieceAssignments(gearpieceAssignments);

            // stage 2: assign gearpieces according to solution
            var assignableGearpieces = assignableGearsets.SelectMany(g => g.Gearpieces).ToList();

            logger.Info($"Making up to \"{assignments.Count}\" item assignments");
            var updatedGearpieces = ItemAssigner.MakeItemAssignments(assignments, assignableGearpieces, itemData);

            // stage 3: prepare prerequisite lists after gearpiece stage
            // group prerequisite items by gearpiece
            prerequisiteGroups.AddRange(groupPrerequisites(allGearsets));
            // filter item list to only needed items
            prerequisiteCandidateItems = prerequisiteCandidateItems
                .Where(item => prerequisiteGroups.Any(g => g.NeedsItemId(item.ItemId)))
                .ToList();

            // stage 4: assign remaining valid items as prerequisites
            var prerequisiteAssignments = solveAndAssignPrerequisites(assignPrerequisiteMateria);

            GearpiecesResult = new()
            {
                Assignments = gearpieceAssignments,
                Edges = gearpieceEdges,
                CandidateItems = gearpieceCandidateItems,
                AssignmentGroups = gearpieceGroups
                    .Select(g => (IAssignmentGroup)g)
                    .ToList()
            };

            PrerequisitesResult = new()
            {
                Assignments = prerequisiteAssignments,
                Edges = prerequisiteEdges ?? new int[0, 0],
                CandidateItems = prerequisiteCandidateItems,
                AssignmentGroups = prerequisiteGroups
                    .Select(g => (IAssignmentGroup)g)
                    .ToList()
            };

#if DEBUG
            logger.Debug("Item Assignment Solver Solution");

            logger.Debug("Gearpiece Assignments");
            logResult();

            logger.Debug("Prerequisite Assignments");
            logResult(logPrereqs: true);
#endif

            return updatedGearpieces;
        }

        private void unassignPrereqs()
        {
            foreach (var gearset in assignableGearsets)
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    if (gearpiece.IsCollected)
                        continue;

                    if (gearpiece.PrerequisiteTree is not IPrerequisiteNode node)
                        continue;

                    if (node.CollectLock)
                        continue;

                    node.IsCollected = false;
                }
        }

        private int[] solveAndAssignPrerequisites(bool assignPrerequisiteMateria)
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
                candidateItemList.RemoveAt(0);
                var itemIdx = prerequisiteCandidateItems.IndexOf(itemToAssign);

                // find prereq group with highest edge score for this item
                var bestAssignScore = NoEdgeWeightValue;
                PrerequisiteAssignmentGroup? bestGroup = null;
                var bestGroupIdx = -1;
                for (var groupIdx = 0; groupIdx < prerequisiteGroups.Count; groupIdx++)
                {
                    var prereqGroup = prerequisiteGroups[groupIdx];
                    var assignScore = prereqGroup.CandidateEdgeWeight(itemToAssign.ItemId, []);

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
                    var oldAssignedItems = bestGroup.AssignItem(itemToAssign, itemData, materiaFactory, assignPrerequisiteMateria);
                    candidateItemList.AddRange(oldAssignedItems);
                }
            }
            while (loopCount++ < maxLoops);

            if (loopCount >= maxLoops)
                logger.Warning($"Max prerequisite solve loop count reached with \"{prerequisiteCandidateItems.Count}\" items unprocessed");

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
                    var candidateMateriaIds = itemData.GetItemMateriaIds(candidate);
                    var candidateMateria = new MateriaGroup(
                        candidateMateriaIds.Select(id => materiaFactory.Create(id))
                        );
                    var edgeWeight = group.CandidateEdgeWeight(candidate.ItemId, candidateMateria);
                    gearpieceEdges[groupIdx, candIdx] = edgeWeight;
                }
            }
        }

        private List<GearpieceAssignmentGroup> groupGearpieces(IEnumerable<Gearset> gearsets)
        {
            var gearpieceGroups = new List<GearpieceAssignmentGroup>();
            var overallGearpieceIdx = -1;

            foreach (var gearset in gearsets)
            {
                foreach (var gearpiece in gearset.Gearpieces)
                {
                    // do not consider manually collected items in solver
                    if (gearpiece.CollectLock)
                        continue;

                    overallGearpieceIdx++;
                    // try to add gearpiece to existing group
                    if (gearpieceGroups.Any(group => group.AddMatchingGearpiece(gearpiece, gearset))) continue;

                    // if no group was found, create a new one and add this gearpiece
                    gearpieceGroups.Add(new GearpieceAssignmentGroup(logger, gearpiece, gearset, overallGearpieceIdx));
                }
            }

            return gearpieceGroups;
        }

        private List<PrerequisiteAssignmentGroup> groupPrerequisites(IEnumerable<Gearset> gearsets)
        {
            var prerequisiteGroups = new List<PrerequisiteAssignmentGroup>();
            var overallPrereqIdx = -1;

            foreach (var gearset in gearsets)
            {
                for (var gearpieceIdx = 0; gearpieceIdx < gearset.Gearpieces.Count; gearpieceIdx++)
                {
                    var gearpiece = gearset.Gearpieces[gearpieceIdx];

                    // do not consider manually collected items in solver
                    if (gearpiece.CollectLock)
                        continue;

                    // has no prerequisites to potentially assign
                    if (gearpiece.PrerequisiteTree == null)
                        continue;

                    // already assigned in gearpiece assignment solution or is manually collected, don't add this to any group
                    if (assignedGearpieces.Contains(gearpiece) || gearpiece.CollectLock)
                        continue;

                    overallPrereqIdx++;

                    // try to add prerequisite to existing group
                    if (prerequisiteGroups.Any(group => group.AddMatchingGearpiece(gearpiece, gearset)))
                        continue;

                    // if no group was found, create a new one and add this prerequisite
                    prerequisiteGroups.Add(new PrerequisiteAssignmentGroup(
                        logger,
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
                var candidateMateriaIds = itemData.GetItemMateriaIds(candidate);
                assignments.Add(
                    new Assignment
                    {
                        ItemId = candidate.ItemId,
                        ItemMateria = candidateMateriaIds.Select(id => materiaFactory.Create(id)).ToList(),
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
                    .OrderByDescending(g => unassignedGroup.CandidateEdgeWeight(g.ItemId, new MateriaGroup(g.MateriaList)));

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
        //        pluginLog.Verbose($"unassigned group item id: {unassignedGroup.ItemId}. Assigned item ids: {string.Join(", ", prerequisiteAssignments.Select(i => prerequisiteGroups[i].ItemId))}");

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

        //        pluginLog.Verbose($"Valid assigned groups: {validAssignedGroups.Count()}");

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

        //                pluginLog.Verbose($"{unassignedGearpiece.ItemName} valid group: {validAssignedGroup.ItemId}");

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

    public interface IItemAssignmentSolver
    {
        public List<Gearpiece> SolveAndAssign();

        public SolveResult? GearpiecesResult { get; }
        public SolveResult? PrerequisitesResult { get; }
    }
}
