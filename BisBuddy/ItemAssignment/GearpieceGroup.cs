using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class GearpieceGroup : IDemandGroup
    {
        // #0: ensure that if a gearpiece is manually collected, it is extremely highly-valued by auto-solver
        private static readonly int ManualCollectionScore = 100000;
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

        public DemandGroupType Type => DemandGroupType.Gearpiece;

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

        public GearpieceGroup(
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

        public bool AddMatchingGearpiece(Gearpiece gearpiece, Gearset gearset)
        {
            // tries to add gearpiece and return true. Returns false if gearpiece doesn't match this group

            // item ids don't match
            if (gearpiece.ItemId != ItemId) return false;

            // item materia don't match
            if (
                !gearpiece
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
            Gearpieces.Add(gearpiece);
            Gearsets.Add(gearset);
            IsManuallyCollected |= gearpiece.IsManuallyCollected;

            return true;
        }

        // the edge score from candidate->gearpiece group. Values materia count first, then materia stat quantity
        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria)
        {
            // candidate item id must match this group's item id, else no edge
            if (candidateId != ItemId) return ItemAssigmentSolver.NoEdgeWeightValue;

            // assign itemCandidateMateria to group materia, 1-1, preserving duplicates
            var materiaInCommon = Materia.GetMatchingMateria(MateriaList, candidateMateria.Select(m => m.ItemId).ToList());

            // get the sub-scores from the common materia etc.
            var manuallyCollectedScore = IsManuallyCollected ? ManualCollectionScore : 0;
            var materiaCountScore = materiaInCommon.Count * MateriaCountScoreScalar;
            var groupSizeScore = Math.Min(Gearpieces.Count * GroupSizeScoreScalar, MaxGroupSizeScore);
            var gearpieceIdxScore = minGearpieceIdx * GearpieceIndexScoreScalar;

            // return sum of sub-scores
            // 0 << 1 < 2 < 3
            return manuallyCollectedScore + materiaCountScore + groupSizeScore + gearpieceIdxScore;
        }
    }
}
