using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequesites
{
    [Serializable]
    public class PrerequesiteAtomNode : PrerequesiteNode
    {
        private bool isManuallyCollected = false;
        private bool isCollected = false;

        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequesiteNodeSourceType SourceType { get; set; }
        public List<PrerequesiteNode> PrerequesiteTree { get; set; }

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
            IsCollected || (PrerequesiteTree.Count > 0 && PrerequesiteTree.All(p => p.IsObtainable));

        public PrerequesiteAtomNode(
            uint itemId,
            string itemName,
            List<PrerequesiteNode>? prerequesiteTree,
            PrerequesiteNodeSourceType sourceType,
            bool isCollected = false,
            bool isManuallyCollected = false
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            PrerequesiteTree = prerequesiteTree ?? [];
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

            foreach (var prereq in PrerequesiteTree)
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

            return PrerequesiteTree.Sum(p => p.ItemNeededCount(itemId, ignoreCollected)); // Need all (max of 1 anyway)
        }

        public int MinRemainingItems()
        {
            if (IsCollected)
                return 0;

            if (PrerequesiteTree.Count == 0)
                return 1;

            return PrerequesiteTree.Sum(p => p.MinRemainingItems()); // Need all (max of 1 anyway)
        }

        public void AddNeededItemIds(Dictionary<uint, int> neededCounts)
        {
            if (neededCounts.TryGetValue(ItemId, out var neededCount))
                neededCounts[ItemId] = neededCount + 1;
            else
                neededCounts[ItemId] = 1;

            foreach (var prereq in PrerequesiteTree)
                prereq.AddNeededItemIds(neededCounts);
        }

        public int PrerequesiteCount()
        {
            return 1 + PrerequesiteTree.Sum(p => p.PrerequesiteCount());
        }

        public string GroupKey()
        {
            return $"""
                ATOM {ItemId} {IsCollected} {IsManuallyCollected} {SourceType}
                {string.Join(" ", PrerequesiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([UNIT] [{PrerequesiteTree.Count}] {ItemName} => \n{string.Join("\n", PrerequesiteTree.Select(p => p.ToString()))})";
        }
    }
}
