using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.ItemAssignment
{
    public class PrerequesiteGroup : IDemandGroup
    {
        // ensure that item->prerequesite group edges are always scored lower than item->gearpiece group edges
        // basically, assign items first, then 'fill in' prerequesites
        private static readonly int PrerequesiteScoreOffset = -100000;
        // #0: ensure that if a prereq is manually collected, it is extremely highly-valued by auto-solver
        private static readonly int ManualCollectionScore = 50000;
        // #1: prioritize prereqs that satisfy many prereqs, primary and secondary/child
        private static readonly int PrereqQuantityScoreScalar = 500;
        // #2: prioritize assigning prereqs to things close to being completed
        private static readonly int MissingPrereqsScalar = -50;
        // #3: prioritize filling gearpiece prereqs closer to the start/top of the list
        private static readonly int GearpieceIndexScoreScalar = -1;

        public DemandGroupType Type => DemandGroupType.Prerequesite;
        public List<GearpiecePrerequesite> GearpiecePrerequesites { get; set; }
        public uint ItemId { get; set; }
        private readonly Dictionary<uint, int> itemIdPrereqCounts = [];
        public bool IsManuallyCollected { get; set; } = false;

        public readonly int minGearpieceIdx;
        public int minRemainingPrereqs;
        private List<Materia> materiaList = [];
        private readonly HashSet<Gearset> gearsets = [];

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

        public PrerequesiteGroup(
            GearpiecePrerequesite gearpiecePrerequesite,
            List<Materia> gearpieceMateria,
            int gearpieceIdx,
            Gearpiece gearpiece,
            Gearset gearset
            )
        {
            ItemId = gearpiecePrerequesite.ItemId;
            GearpiecePrerequesites = [gearpiecePrerequesite];
            gearsets = [gearset];
            MateriaList = new List<Materia>(gearpieceMateria);
            IsManuallyCollected = gearpiecePrerequesite.IsManuallyCollected;
            minGearpieceIdx = gearpieceIdx;
            minRemainingPrereqs = gearpiece.PrerequisiteItems.Where(p => !p.IsCollected).Count();

            itemIdPrereqCounts.Add(gearpiecePrerequesite.ItemId, gearpiecePrerequesite.PrerequesiteCount + 1);
            addQuantityCounts(gearpiecePrerequesite);
        }

        private void addQuantityCounts(GearpiecePrerequesite prereqItem)
        {
            foreach (var prereq in prereqItem.Prerequesites)
            {
                // add that this item is needed (and to the "level" its needed)
                itemIdPrereqCounts[prereq.ItemId] = prereq.PrerequesiteCount + 1;

                // propogate update to children
                addQuantityCounts(prereq);
            }
        }

        public bool AddMatchingPrerequesite(GearpiecePrerequesite prereq, Gearpiece prereqGearpiece, Gearset gearset)
        {
            // tries to add prereq and return true.
            // Returns false if prereq doesn't match this group

            // item ids don't match
            if (prereq.ItemId != ItemId) return false;

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

            if (gearsets.Contains(gearset))
            {
                // gearset already added, return false
                return false;
            }

            // ids and materia match, add it to group and return true
            GearpiecePrerequesites.Add(prereq);
            gearsets.Add(gearset);
            addQuantityCounts(prereq);
            IsManuallyCollected |= prereq.IsManuallyCollected;
            minRemainingPrereqs = Math.Min(prereqGearpiece.PrerequisiteItems.Where(p => !p.IsCollected).Count(), minRemainingPrereqs);

            return true;
        }

        public int CandidateEdgeWeight(GameInventoryItem candidate)
        {
            var candidateId = ItemData.GameInventoryItemId(candidate);

            var prereqsSatisfiedByCandidate = itemIdPrereqCounts.GetValueOrDefault(candidateId, 0);

            if (prereqsSatisfiedByCandidate == 0) return ItemAssigmentSolver.NoEdgeWeightValue;

            // get the sub-scores for the prerequesite group
            var manuallyCollectedScore = IsManuallyCollected ? ManualCollectionScore : 0;
            var prereqQuantityScore = prereqsSatisfiedByCandidate * PrereqQuantityScoreScalar;
            var gearpiecePrereqsMissingPenalty = minRemainingPrereqs * MissingPrereqsScalar;
            var gearpieceIndexPenalty = minGearpieceIdx * GearpieceIndexScoreScalar;

            // return sum of sub-scores
            return PrerequesiteScoreOffset + manuallyCollectedScore + prereqQuantityScore + gearpiecePrereqsMissingPenalty + gearpieceIndexPenalty;
        }
    }
}
