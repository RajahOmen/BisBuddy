using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteAtomNode : IPrerequisiteNode
    {
        private List<IPrerequisiteNode> prerequisiteTree;
        private bool collectLock = false;
        private bool isCollected = false;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsMeldable { get; set; } = false;
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public IReadOnlyList<IPrerequisiteNode> PrerequisiteTree
        {
            get => prerequisiteTree;
            set
            {
                foreach (var node in prerequisiteTree)
                    node.OnPrerequisiteChange -= OnPrerequisiteChange;

                foreach (var node in value)
                    node.OnPrerequisiteChange += OnPrerequisiteChange;

                prerequisiteTree = value.ToList();
            }
        }

        public bool IsCollected
        {
            get => isCollected;
            set {
                if (value == isCollected)
                    return;

                if (CollectLock)
                    throw new InvalidOperationException($"Cannot {(value ? "collect" : "uncollect")} atom prereq {ItemId}, is locked.");

                isCollected = value;
                foreach (var prereq in PrerequisiteTree)
                    if (!prereq.CollectLock)
                        prereq.IsCollected = value;

                triggerPrerequisiteChange();
            }
        }
        public bool CollectLock
        {
            get => collectLock;
            set
            {
                if (value == collectLock)
                    return;

                collectLock = value;
                triggerPrerequisiteChange();
            }
        }

        public void SetIsCollectedLocked(bool toCollect)
        {
            if (IsCollected == toCollect && CollectLock)
                return;

            if (!CollectLock)
                CollectLock = true;

            isCollected = toCollect;
            foreach (var prereq in PrerequisiteTree)
                prereq.SetIsCollectedLocked(toCollect);
            triggerPrerequisiteChange();
        }
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];
        public IEnumerable<ItemRequirement> ItemRequirements
        {
            get
            {
                yield return new ItemRequirement()
                {
                    ItemId = ItemId,
                    CollectionStatus = CollectionStatus,
                    RequirementType = RequirementType.Prerequisite,
                };

                if (PrerequisiteTree.Count > 0)
                    foreach (var requirement in PrerequisiteTree[0].ItemRequirements)
                        yield return requirement;
            }
        }


        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return CollectionStatusType.ObtainedComplete;
                if (PrerequisiteTree.Count > 0 && PrerequisiteTree.All(
                    p => p.CollectionStatus >= CollectionStatusType.Obtainable))
                    return CollectionStatusType.Obtainable;
                return CollectionStatusType.NotObtainable;
            }
        }
        public PrerequisiteAtomNode(
            uint itemId,
            string itemName,
            List<IPrerequisiteNode>? prerequisiteTree,
            PrerequisiteNodeSourceType sourceType,
            bool isCollected = false,
            bool collectLock = false,
            string? nodeId = null,
            bool isMeldable = false
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            this.prerequisiteTree = prerequisiteTree ?? [];
            this.isCollected = isCollected;
            this.collectLock = collectLock;
            IsMeldable = isMeldable;

            foreach (var prereq in PrerequisiteTree)
                prereq.OnPrerequisiteChange += handlePrereqChange;
        }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;

        private void handlePrereqChange() =>
            triggerPrerequisiteChange();

        private void triggerPrerequisiteChange() =>
            OnPrerequisiteChange?.Invoke();

        public void AddNode(IPrerequisiteNode node) =>
            InsertNode(PrerequisiteTree.Count, node);

        public void ReplaceNode(int index, IPrerequisiteNode newNode)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, PrerequisiteTree.Count);

            var oldNode = PrerequisiteTree[index];
            prerequisiteTree[index] = newNode;

            oldNode.OnPrerequisiteChange -= handlePrereqChange;
            newNode.OnPrerequisiteChange += handlePrereqChange;
        }

        public void InsertNode(int index, IPrerequisiteNode node)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, PrerequisiteTree.Count);

            prerequisiteTree.Insert(index, node);
            node.OnPrerequisiteChange += handlePrereqChange;
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

        public IPrerequisiteNode? AssignItemId(uint itemId)
        {
            if (IsCollected)
                return null;

            if (ItemId == itemId)
            {
                isCollected = true;
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

        public List<uint> CollectLockItemIds()
        {
            if (CollectLock)
                return [ItemId];

            return PrerequisiteTree
                .SelectMany(p => p.CollectLockItemIds())
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
                ATOM {ItemId} {IsCollected} {CollectLock} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            var childrenStr = PrerequisiteTree.Count > 0
                ? $"=>\n    {string.Join("\n", PrerequisiteTree.Select(p => p.ToString()))}"
                : "";
            return $"[UNIT] {ItemName}{childrenStr}";
        }
    }
}
