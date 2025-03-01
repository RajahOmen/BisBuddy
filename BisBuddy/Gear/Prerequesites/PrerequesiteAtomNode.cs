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

        public uint ItemId { get; set; }
        public string ItemName { get; set; }
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

        public PrerequisiteAtomNode(
            uint itemId,
            string itemName,
            List<PrerequisiteNode>? prerequisiteTree,
            PrerequisiteNodeSourceType sourceType,
            bool isCollected = false,
            bool isManuallyCollected = false
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            PrerequisiteTree = prerequisiteTree ?? [];
            IsCollected = isCollected;
            IsManuallyCollected = isManuallyCollected;
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            if (IsManuallyCollected && !collected && !manualToggle)
            {
                Services.Log.Error($"Cannot automatically uncollect manually collected item: {ItemName}");
                return;
            }

            //groups = null;

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

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequisiteTree.Count == 0)
                return 1;

            return PrerequisiteTree.Sum(p => p.MinRemainingItems()); // Need all (max of 1 anyway)
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            if (neededCounts.TryGetValue(ItemId, out var neededCount))
                neededCounts[ItemId] = neededCount + 1;
            else
                neededCounts[ItemId] = 1;

            foreach (var prereq in PrerequisiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequisiteCount()
        {
            return 1 + PrerequisiteTree.Sum(p => p.PrerequisiteCount());
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
