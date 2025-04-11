using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class PrerequisiteAssignmentGroup : IAssignmentGroup
    {
        // #0: If this would complete this prereq, value it highly
        private static readonly int FinishesPrereqBonus = 100000;
        // #1: prioritize filling prereqs at a higher depth (less nested)
        private static readonly int PrereqDepthPenalty = -10000;
        // #2: prioritize assigning to groups that satisfy a lot of gearpieces
        private static readonly int PrereqGroupSizeScalar = 1000;
        // #3: prioritize assigning to items that need few of the candidate item over ones that require many
        private static readonly int PrereqQuantityScorePenalty = -500;
        // #4: prioritize assigning prereqs to things close to being completed
        private static readonly int MissingPrereqsScalar = -50;
        // #5: prioritize filling gearpiece prereqs closer to the start/top of the list
        private static readonly int GearpieceIndexScoreScalar = -1;

        public AssignmentGroupType Type => AssignmentGroupType.Prerequisite;
        public List<Gearpiece> Gearpieces { get; set; }
        public readonly bool StrictMateriaMatching;
        public uint ItemId { get; set; }
        private readonly Dictionary<uint, (int MinDepth, int Count)> neededItemIds;

        public readonly int minGearpieceIdx;
        private List<Materia> materiaList = [];
        public readonly HashSet<Gearset> Gearsets = [];

        public Dictionary<Gearpiece, HashSet<(PrerequisiteNode Node, GameInventoryItem Item)>> DirectlyAssignedNodes = [];

        public List<Materia> MateriaList
        {
            get => materiaList;
            set
            {
                materiaList = value;
                // sort by highest id first (rough approx of StatQuantity)
                materiaList.Sort((m1, m2) => m2.ItemId.CompareTo(m1.ItemId));
            }
        }

        public PrerequisiteAssignmentGroup(
            Gearpiece gearpiece,
            Gearset gearset,
            int gearpieceIdx,
            bool strictMateriaMatching
            )
        {
            ItemId = gearpiece.ItemId;
            Gearpieces = [gearpiece];
            Gearsets = [gearset];
            MateriaList = new List<Materia>(gearpiece.ItemMateria);
            minGearpieceIdx = gearpieceIdx;
            StrictMateriaMatching = strictMateriaMatching;

            neededItemIds = [];
            gearpiece.PrerequisiteTree?.AddNeededItemIds(neededItemIds);
        }

        public bool NeedsItemId(uint candidateItemId)
        {
            if (!neededItemIds.TryGetValue(candidateItemId, out var needed))
                return false;

            return needed.Count > 0;
        }

        public List<GameInventoryItem> AssignItem(GameInventoryItem item)
        {
            if (Gearpieces.Count == 0)
                return [];

            List<GameInventoryItem> shadowedAssignments = [];

            foreach (var gearpiece in Gearpieces)
            {
                if (gearpiece.PrerequisiteTree == null)
                    continue;

                var nodeAssigned = gearpiece.PrerequisiteTree.AssignItemId(item.ItemId);

                if (nodeAssigned == null)
                    continue;

                var previousAssignments = DirectlyAssignedNodes.GetValueOrDefault(gearpiece, []);
                previousAssignments.Add((nodeAssigned, item));

                var childNodeIds = nodeAssigned.ChildNodeIds;
                var childAssignments = previousAssignments
                    .Where(assign =>
                        childNodeIds.Contains(assign.Node.NodeId)
                        );

                DirectlyAssignedNodes[gearpiece] = previousAssignments
                    .Except(childAssignments)
                    .ToHashSet();

                shadowedAssignments.AddRange(childAssignments.Select(assign => assign.Item));
            }

            // remake needed dictionary after item assignment(s)
            neededItemIds.Clear();
            Gearpieces.ForEach(gearpiece => gearpiece.PrerequisiteTree?.AddNeededItemIds(neededItemIds));

            return shadowedAssignments;
        }

        public bool AddMatchingGearpiece(Gearpiece gearpiece, Gearset gearset)
        {
            // tries to add prereq and return true.
            // Returns false if prereq doesn't match this group

            // item ids don't match
            if (gearpiece.ItemId != ItemId)
                return false;

            if (gearpiece.PrerequisiteTree == null)
                throw new Exception($"Expected PrerequisiteGroup for gearpiece \"{gearpiece.ItemName}\", got null");

            // strict materia matching is enabled & item materia don't match
            if (
                StrictMateriaMatching
                && !Materia.MateriaListCanSatisfy(MateriaList, gearpiece.ItemMateria)
                )
                return false;

            // if gearset already added, return false
            if (Gearsets.Contains(gearset))
                return false;

            // ids and materia match, add it to group and return true
            Gearpieces.Add(gearpiece);
            Gearsets.Add(gearset);
            gearpiece.PrerequisiteTree.AddNeededItemIds(neededItemIds);

            // gearpiece has MORE Materia required than on current group, overwrite
            if (gearpiece.ItemMateria.Count > MateriaList.Count)
                MateriaList = new List<Materia>(gearpiece.ItemMateria);

            return true;
        }

        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria)
        {
            if (!neededItemIds.TryGetValue(candidateId, out var neededData))
                return ItemAssigmentSolver.NoEdgeWeightValue;

            var remainingPrereqs = Gearpieces
                .Select(g => g.PrerequisiteTree?.MinRemainingItems(candidateId) ?? 1000)
                .Min();

            // get the sub-scores for the prerequisite group
            var subScores = new Dictionary<string, int>()
            {
                {
                    "gearpiecesPrereqsMissing",
                    remainingPrereqs == 0
                        ? FinishesPrereqBonus // finishes the prereqs, add bonus
                        : remainingPrereqs * MissingPrereqsScalar // doesn't finish prereqs, penalize with missing amount
                },
                {
                    "prereqDepthPenalty",
                    neededData.MinDepth * PrereqDepthPenalty
                },
                {
                    "prereqGroupSizeScore",
                    Gearpieces.Count * PrereqGroupSizeScalar
                },
                {
                    "prereqQuantityScore",
                    (neededData.Count * PrereqQuantityScorePenalty) / Gearpieces.Count
                },
                {
                    "gearpieceIndexPenalty",
                    minGearpieceIdx * GearpieceIndexScoreScalar
                }
            };

            var totalScore = subScores.Values.Sum();

#if DEBUG
            var subScoreLog = string.Join("\n", subScores.Select(subScore => $"{subScore.Key}: {subScore.Value}"));
            Services.Log.Verbose($"prereq group item id: {ItemId}. Candidate item id: {candidateId}\n{subScoreLog}\ntotal score: {totalScore}");
#endif

            // return sum of sub-scores
            return totalScore;
        }
    }
}
