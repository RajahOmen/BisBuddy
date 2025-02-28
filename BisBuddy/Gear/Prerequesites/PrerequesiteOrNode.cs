using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequesites
{
    [Serializable]
    public class PrerequesiteOrNode : PrerequesiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequesiteNodeSourceType SourceType { get; set; }
        public List<PrerequesiteNode> PrerequesiteTree { get; set; }

        public bool IsCollected => PrerequesiteTree.Any(p => p.IsCollected);
        public bool IsManuallyCollected => PrerequesiteTree.Any(p => p.IsManuallyCollected);
        public bool IsObtainable => IsCollected || PrerequesiteTree.Any(p => p.IsObtainable);

        public PrerequesiteOrNode(
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

            return PrerequesiteTree.Max(p => p.ItemNeededCount(itemId, ignoreCollected));   // only need one, pick max needed
        }

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequesiteTree.Count == 0)
                return 1;

            return PrerequesiteTree.Min(p => p.MinRemainingItems());   // only need one, pick min needed
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            foreach (var prereq in PrerequesiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequesiteCount()
        {
            // ignore this group, it isn't a real prerequesite
            return
                PrerequesiteTree.Sum(p => p.PrerequesiteCount());
        }

        public string GroupKey()
        {
            return $"""
                OR {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequesiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([OR] [{PrerequesiteTree.Count}] {string.Join("\n", PrerequesiteTree.Select(p => p.ToString()))})";
        }
    }
}
