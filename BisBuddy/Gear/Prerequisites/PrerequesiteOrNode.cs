using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteOrNode : PrerequisiteNode
    {
        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }

        [JsonIgnore]
        public bool IsCollected => PrerequisiteTree.Any(p => p.IsCollected);
        [JsonIgnore]
        public bool IsManuallyCollected => PrerequisiteTree.Any(p => p.IsManuallyCollected);
        [JsonIgnore]
        public bool IsObtainable => IsCollected || PrerequisiteTree.Any(p => p.IsObtainable);
        [JsonIgnore]
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];

        public PrerequisiteOrNode(
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
            return PrerequisiteTree.Max(p => p.ItemNeededCount(itemId, ignoreCollected) as int?) ?? 0;   // only need one, pick max needed
        }

        public int MinRemainingItems(uint? newItemId = null)
        {
            return PrerequisiteTree.Min(p => p.MinRemainingItems(newItemId) as int?) ?? 0;   // only need one, pick min needed
        }

        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0)
        {
            // don't need anything if OR is already satisfied
            if (IsCollected)
                return;

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
            // don't assign if OR is already satisfied
            if (IsCollected)
                return null;

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
