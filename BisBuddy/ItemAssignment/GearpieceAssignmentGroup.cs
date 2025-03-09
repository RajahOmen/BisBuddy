using BisBuddy.Gear;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class GearpieceAssignmentGroup : IAssignmentGroup
    {
        // #1: maximize materia count in common
        private static readonly int MateriaCountScoreScalar = 15000;
        // #2: maximize group size. has a max value to prevent group size from dominating score
        private static readonly int GroupSizeScoreScalar = 1000;
        private static readonly int MaxGroupSizeScore = 10 * GroupSizeScoreScalar; // 10 gearpieces max
        // #3: prioritize filling gearpieces closer to the start/top of the list
        private static readonly int GearpieceIndexScoreScalar = -1;

        private List<Materia> materiaList = [];
        public readonly HashSet<Gearset> Gearsets = [];
        private readonly int minGearpieceIdx;

        public AssignmentGroupType Type => AssignmentGroupType.Gearpiece;

        // the item id of the item this gearpiece group is for
        public uint ItemId { get; set; }

        // materia list with ids sorted (to ensure easy SequenceEqual comparison)
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

        public List<Gearpiece> Gearpieces { get; set; } = [];

        public bool IsManuallyCollected { get; set; } = false;

        public GearpieceAssignmentGroup(
            Gearpiece gearpiece,
            Gearset gearset,
            int gearpieceIdx
            )
        {
            ItemId = gearpiece.ItemId;
            MateriaList = new List<Materia>(gearpiece.ItemMateria);
            Gearpieces = [gearpiece];
            Gearsets = [gearset];
            minGearpieceIdx = gearpieceIdx;
            IsManuallyCollected = gearpiece.IsManuallyCollected;
        }

        public bool NeedsItemId(uint candidateItemId)
        {
            return ItemId == candidateItemId;
        }

        public bool AddMatchingGearpiece(Gearpiece gearpiece, Gearset gearset)
        {
            // tries to add gearpiece and return true. Returns false if gearpiece doesn't match this group

            // item ids don't match
            if (gearpiece.ItemId != ItemId)
                return false;

            // item materia don't match
            if (!Materia.MateriaListCanSatisfy(MateriaList, gearpiece.ItemMateria))
                return false;

            // gearset already added, return false
            if (Gearsets.Contains(gearset))
                return false;

            // ids and materia match, add it to group and return true
            Gearpieces.Add(gearpiece);
            Gearsets.Add(gearset);
            IsManuallyCollected |= gearpiece.IsManuallyCollected;

            // gearpiece has MORE Materia required than on current group, overwrite
            if (gearpiece.ItemMateria.Count > MateriaList.Count)
                MateriaList = new List<Materia>(gearpiece.ItemMateria);

            return true;
        }

        // the edge score from candidate->gearpiece group. Values materia count first, then materia stat quantity
        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria)
        {
            // candidate item id must match this group's item id, else no edge
            if (candidateId != ItemId)
                return ItemAssigmentSolver.NoEdgeWeightValue;

            // assign itemCandidateMateria to group materia, 1-1, preserving duplicates
            var materiaInCommon = Materia.GetMatchingMateria(MateriaList, candidateMateria.Select(m => m.ItemId).ToList());

            // get the sub-scores from the common materia etc.
            var subScores = new Dictionary<string, int>()
            {
                {
                    "materiaCountScore",
                    materiaInCommon.Count * MateriaCountScoreScalar
                },
                {
                    "groupSizeScore",
                    Math.Min(Gearpieces.Count * GroupSizeScoreScalar, MaxGroupSizeScore)
                },
                {
                    "gearpieceIdxScore",
                    minGearpieceIdx * GearpieceIndexScoreScalar
                },
            };

            var totalScore = subScores.Values.Sum();

#if DEBUG
            var subScoreLog = string.Join("\n", subScores.Select(subScore => $"{subScore.Key}: {subScore.Value}"));
            Services.Log.Verbose($"gearpiece group item id: {ItemId}. Candidate item id: {candidateId}\n{subScoreLog}\ntotal score: {totalScore}");
#endif

            // return sum of sub-scores
            return totalScore;
        }
    }
}
