using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class PrerequisiteAssignmentGroup : IAssignmentGroup
    {
        // ensure that item->prerequisite group edges are always scored lower than item->gearpiece group edges
        // basically, assign items first, then 'fill in' prerequisites
        private static readonly int PrerequisiteScoreOffset = -100000;
        // #0: ensure that if a prereq is manually collected, it is extremely highly-valued by auto-solver
        private static readonly int ManualCollectionScore = 50000;
        // #1: prioritize prereqs that satisfy many prereqs, primary and secondary/child
        private static readonly int PrereqQuantityScoreScalar = 500;
        // #2: prioritize assigning prereqs to things close to being completed
        private static readonly int MissingPrereqsScalar = -50;
        // #3: prioritize filling gearpiece prereqs closer to the start/top of the list
        private static readonly int GearpieceIndexScoreScalar = -1;

        public AssignmentGroupType Type => AssignmentGroupType.Prerequisite;
        public List<PrerequisiteNode> PrerequisiteGroups { get; set; }
        public uint ItemId { get; set; }
        private readonly Dictionary<uint, int> neededItemIds;
        public bool IsManuallyCollected { get; set; } = false;

        public readonly int minGearpieceIdx;
        public int minRemainingPrereqs;
        private List<Materia> materiaList = [];
        public readonly HashSet<Gearset> Gearsets = [];

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
            PrerequisiteNode prerequisiteGroup,
            List<Materia> gearpieceMateria,
            int gearpieceIdx,
            Gearpiece gearpiece,
            Gearset gearset
            )
        {
            ItemId = prerequisiteGroup.ItemId;
            PrerequisiteGroups = [prerequisiteGroup];
            Gearsets = [gearset];
            MateriaList = new List<Materia>(gearpieceMateria);
            IsManuallyCollected = prerequisiteGroup.IsManuallyCollected;
            minGearpieceIdx = gearpieceIdx;

            neededItemIds = [];
            prerequisiteGroup.AddNeededItemIds(neededItemIds);
        }

        public bool AddMatchingPrerequisite(PrerequisiteNode prereq, Gearpiece prereqGearpiece, Gearset gearset)
        {
            // tries to add prereq and return true.
            // Returns false if prereq doesn't match this group

            // item ids don't match
            if (prereq.ItemId != ItemId)
                return false;

            if (prereqGearpiece.PrerequisiteTree == null)
                throw new Exception($"Expected PrerequisiteGroup for gearpiece \"{prereqGearpiece.ItemName}\", got null");

            // item materia don't match
            if (
                !prereqGearpiece
                .ItemMateria
                .OrderByDescending(m => m.ItemId)
                .Select(m => m.ItemId)
                .SequenceEqual(
                    MateriaList
                    .Select(m => m.ItemId)
                    )
                )
            {
                return false;
            }

            if (Gearsets.Contains(gearset))
            {
                // gearset already added, return false
                return false;
            }

            // ids and materia match, add it to group and return true
            PrerequisiteGroups.Add(prereq);
            Gearsets.Add(gearset);
            prereqGearpiece.PrerequisiteTree.AddNeededItemIds(neededItemIds);
            IsManuallyCollected |= prereq.IsManuallyCollected;
            minRemainingPrereqs = Math.Min(
                prereqGearpiece.PrerequisiteTree.MinRemainingItems(),
                minRemainingPrereqs
                );

            return true;
        }

        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria)
        {
            var prereqsSatisfiedByCandidate = neededItemIds.GetValueOrDefault(candidateId, 0);

            if (prereqsSatisfiedByCandidate == 0) return ItemAssigmentSolver.NoEdgeWeightValue;

            // get the sub-scores for the prerequisite group
            var manuallyCollectedScore = IsManuallyCollected ? ManualCollectionScore : 0;
            var prereqQuantityScore = prereqsSatisfiedByCandidate * PrereqQuantityScoreScalar;
            var gearpiecePrereqsMissingPenalty = minRemainingPrereqs * MissingPrereqsScalar;
            var gearpieceIndexPenalty = minGearpieceIdx * GearpieceIndexScoreScalar;

            // return sum of sub-scores
            return PrerequisiteScoreOffset + manuallyCollectedScore + prereqQuantityScore + gearpiecePrereqsMissingPenalty + gearpieceIndexPenalty;
        }
    }
}
