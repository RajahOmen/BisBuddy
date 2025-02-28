using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequesites
{
    [Serializable]
    public class PrerequesiteAndNode : PrerequesiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequesiteNodeSourceType SourceType { get; set; }
        public List<PrerequesiteNode> PrerequesiteTree { get; set; }

        public bool IsCollected => PrerequesiteTree.All(p => p.IsCollected);
        public bool IsManuallyCollected => PrerequesiteTree.All(p => p.IsManuallyCollected);
        public bool IsObtainable => IsCollected || PrerequesiteTree.All(p => p.IsObtainable);

        public List<(PrerequesiteNode Node, int Count)> Groups()
        {
            return PrerequesiteTree
                .GroupBy(p => p.GroupKey())
                .Select(g => (g.First(), g.Count()))
                .OrderBy(g => g.Item1.IsCollected)
                .ThenBy(g => g.Item1.IsObtainable)
                .ToList();
        }

        public PrerequesiteAndNode(
            uint itemId,
            string itemName,
            List<PrerequesiteNode>? prerequesiteTree,
            PrerequesiteNodeSourceType sourceType
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            PrerequesiteTree = prerequesiteTree ?? [];
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            foreach (var p in PrerequesiteTree)
                p.SetCollected(collected, manualToggle);
        }

        public int ItemNeededCount(uint itemId, bool ignoreCollected)
        {
            if (ignoreCollected && IsCollected)
                return 0;

            if (ItemId == itemId)
                return 1;

            return PrerequesiteTree.Sum(p => p.ItemNeededCount(itemId, ignoreCollected));   // need all
        }

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequesiteTree.Count == 0)
                return 1;

            return PrerequesiteTree.Sum(p => p.MinRemainingItems());   // need all
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            foreach (var prereq in PrerequesiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequesiteCount()
        {
            // ignore this group, it isn't a real prerequesite
            return PrerequesiteTree.Sum(p => p.PrerequesiteCount());
        }

        public string GroupKey()
        {
            return $"""
                AND {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequesiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([AND] [{PrerequesiteTree.Count}] {string.Join("\n", PrerequesiteTree.Select(p => p.ToString()))})";
        }
    }
}
