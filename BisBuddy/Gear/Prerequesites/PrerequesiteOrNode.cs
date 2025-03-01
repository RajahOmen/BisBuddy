using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteOrNode : PrerequisiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }

        public bool IsCollected => PrerequisiteTree.Any(p => p.IsCollected);
        public bool IsManuallyCollected => PrerequisiteTree.Any(p => p.IsManuallyCollected);
        public bool IsObtainable => IsCollected || PrerequisiteTree.Any(p => p.IsObtainable);

        public PrerequisiteOrNode(
            uint itemId,
            string itemName,
            List<PrerequisiteNode>? prerequisiteTree,
            PrerequisiteNodeSourceType sourceType
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            PrerequisiteTree = prerequisiteTree ?? [];
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            foreach (var p in PrerequisiteTree)
                p.SetCollected(collected, manualToggle);
        }

        public int ItemNeededCount(uint itemId, bool ignoreCollected)
        {
            if (ignoreCollected && IsCollected)
                return 0;

            if (ItemId == itemId)
                return 1;

            return PrerequisiteTree.Max(p => p.ItemNeededCount(itemId, ignoreCollected));   // only need one, pick max needed
        }

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequisiteTree.Count == 0)
                return 1;

            return PrerequisiteTree.Min(p => p.MinRemainingItems());   // only need one, pick min needed
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            foreach (var prereq in PrerequisiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequisiteCount()
        {
            // ignore this group, it isn't a real prerequisite
            return
                PrerequisiteTree.Sum(p => p.PrerequisiteCount());
        }

        public string GroupKey()
        {
            return $"""
                OR {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([OR] [{PrerequisiteTree.Count}] {string.Join("\n", PrerequisiteTree.Select(p => p.ToString()))})";
        }
    }
}
