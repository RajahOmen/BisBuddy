using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteAtomNode : PrerequisiteNode
    {
        private bool isManuallyCollected = false;
        private bool isCollected = false;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsMeldable { get; set; } = false;
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }

        public bool IsCollected
        {
            get => isCollected;
            private set => isCollected = value;
        }
        public bool IsManuallyCollected
        {
            get => isManuallyCollected;
            private set => isManuallyCollected = value;
        }
        public bool IsObtainable =>
            IsCollected || (PrerequisiteTree.Count > 0 && PrerequisiteTree.All(p => p.IsObtainable));
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];

        public PrerequisiteAtomNode(
            uint itemId,
            string itemName,
            List<PrerequisiteNode>? prerequisiteTree,
            PrerequisiteNodeSourceType sourceType,
            bool isCollected = false,
            bool isManuallyCollected = false,
            string? nodeId = null,
            bool isMeldable = false
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            PrerequisiteTree = prerequisiteTree ?? [];
            IsCollected = isCollected;
            IsManuallyCollected = isManuallyCollected;
            IsMeldable = isMeldable;
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            // don't error for prerequisites
            if (IsManuallyCollected && !collected && !manualToggle)
                return;

            IsCollected = collected;

            // if toggled by user, set manually collected flag
            if (manualToggle)
                IsManuallyCollected = collected;

            foreach (var prereq in PrerequisiteTree)
            {
                prereq.SetCollected(collected, manualToggle);
            }
        }

        public int ItemNeededCount(uint itemId, bool ignoreCollected)
        {
            if (ignoreCollected && IsCollected)
                return 0;

            if (ItemId == itemId)
                return 1;

            return PrerequisiteTree.Sum(p => p.ItemNeededCount(itemId, ignoreCollected)); // Need all (max of 1 anyway)
        }

        public int MinRemainingItems(uint? newItemId = null)
        {
            if (IsCollected)
                return 0;

            if (newItemId == ItemId)
                return 0;

            if (PrerequisiteTree.Count == 0)
                return 1;

            return PrerequisiteTree.Sum(p => p.MinRemainingItems()); // Need all (max of 1 anyway)
        }

        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0)
        {
            if (neededCounts.TryGetValue(ItemId, out var value))
            {
                var newDepth = Math.Min(value.MinDepth, startDepth);
                neededCounts[ItemId] = (newDepth, value.Count + 1);
            }
            else
                neededCounts[ItemId] = (startDepth, 1);

            foreach (var prereq in PrerequisiteTree)
                prereq.AddNeededItemIds(neededCounts, startDepth + 1);
        }

        public int PrerequisiteCount()
        {
            return 1 + PrerequisiteTree.Sum(p => p.PrerequisiteCount());
        }

        public PrerequisiteNode? AssignItemId(uint itemId)
        {
            if (IsCollected)
                return null;

            if (ItemId == itemId)
            {
                SetCollected(true, false);
                return this;
            }

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
            if (IsManuallyCollected)
                return [ItemId];

            return PrerequisiteTree
                .SelectMany(p => p.ManuallyCollectedItemIds())
                .ToList();
        }

        public HashSet<string> MeldableItemNames()
        {
            var prereqNames = PrerequisiteTree
                .SelectMany(p => p.MeldableItemNames())
                .ToHashSet();

            if (IsMeldable)
                prereqNames.Add(ItemName);

            return prereqNames;
        }

        public string GroupKey()
        {
            return $"""
                ATOM {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([UNIT] [{PrerequisiteTree.Count}] {ItemName} => \n{string.Join("\n", PrerequisiteTree.Select(p => p.ToString()))})";
        }
    }
}
