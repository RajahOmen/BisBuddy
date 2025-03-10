using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteAndNode : PrerequisiteNode
    {
        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }

        [JsonIgnore]
        public bool IsCollected => PrerequisiteTree.All(p => p.IsCollected);
        [JsonIgnore]
        public bool IsManuallyCollected => PrerequisiteTree.All(p => p.IsManuallyCollected);
        [JsonIgnore]
        public bool IsObtainable => IsCollected || PrerequisiteTree.All(p => p.IsObtainable);
        [JsonIgnore]
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];

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
            PrerequisiteNodeSourceType sourceType,
            string? nodeId = null
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
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
            return PrerequisiteTree.Sum(p => p.ItemNeededCount(itemId, ignoreCollected));   // need all
        }

        public int MinRemainingItems(uint? newItemId = null)
        {
            var totalRemainingItems = 0;

            // is new item still available to use for this loop
            var itemAvailable = newItemId != null;
            foreach (var prereq in PrerequisiteTree)
            {
                // calculate how many items remaining with no item provided
                var minNoItem = prereq.MinRemainingItems();
                if (!itemAvailable)
                {
                    totalRemainingItems += minNoItem;
                    continue;
                }

                // calculate how many items remaining with provided item
                var minWithItem = prereq.MinRemainingItems(newItemId);
                if (minNoItem != minWithItem)
                    // if not equal, then must be able to use this item for this prereq
                    // and no longer be able to be used for future prereqs in node
                    itemAvailable = false;

                totalRemainingItems += minWithItem;
            }
            return totalRemainingItems;
        }

        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0)
        {
            foreach (var prereq in PrerequisiteTree)
                prereq.AddNeededItemIds(neededCounts, startDepth);
        }

        public int PrerequisiteCount()
        {
            // ignore this group, it isn't a real prerequisite
            return PrerequisiteTree.Sum(p => p.PrerequisiteCount());
        }

        public PrerequisiteNode? AssignItemId(uint itemId)
        {
            foreach (var prereq in PrerequisiteTree)
            {
                var assignResult = prereq.AssignItemId(itemId);
                if (assignResult != null)
                    return assignResult;
            }

            return null;
        }

        public List<uint> ManuallyCollectedItemIds()
        {
            return PrerequisiteTree
                .SelectMany(p => p.ManuallyCollectedItemIds())
                .ToList();
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
