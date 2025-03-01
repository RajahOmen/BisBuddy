using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteAndNode : PrerequisiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }

        public bool IsCollected => PrerequisiteTree.All(p => p.IsCollected);
        public bool IsManuallyCollected => PrerequisiteTree.All(p => p.IsManuallyCollected);
        public bool IsObtainable => IsCollected || PrerequisiteTree.All(p => p.IsObtainable);

        public List<(PrerequisiteNode Node, int Count)> Groups()
        {
            return PrerequisiteTree
                .GroupBy(p => p.GroupKey())
                .Select(g => (g.First(), g.Count()))
                .OrderBy(g => g.Item1.IsCollected)
                .ThenBy(g => g.Item1.IsObtainable)
                .ToList();
        }

        public PrerequisiteAndNode(
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

            return PrerequisiteTree.Sum(p => p.ItemNeededCount(itemId, ignoreCollected));   // need all
        }

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequisiteTree.Count == 0)
                return 1;

            return PrerequisiteTree.Sum(p => p.MinRemainingItems());   // need all
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            foreach (var prereq in PrerequisiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequisiteCount()
        {
            // ignore this group, it isn't a real prerequisite
            return PrerequisiteTree.Sum(p => p.PrerequisiteCount());
        }

        public string GroupKey()
        {
            return $"""
                AND {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([AND] [{PrerequisiteTree.Count}] {string.Join("\n", PrerequisiteTree.Select(p => p.ToString()))})";
        }
    }
}
