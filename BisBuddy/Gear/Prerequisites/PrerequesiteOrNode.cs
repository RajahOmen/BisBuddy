using FFXIVClientStructs.FFXIV.Common.Lua;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    [Serializable]
    public class PrerequisiteOrNode : IPrerequisiteNode
    {
        private List<IPrerequisiteNode> prerequisiteTree;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
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
            get => PrerequisiteTree.Any(p => p.IsCollected);
            set
            {
                foreach (var prereq in prerequisiteTree)
                    if (!prereq.CollectLock)
                        prereq.IsCollected = value;
            }
        }
        public bool CollectLock
        {
            get => PrerequisiteTree.All(p => p.CollectLock);
            set
            {
                foreach (var prereq in prerequisiteTree)
                    prereq.CollectLock = value;
            }
        }
        public void SetIsCollectedLocked(bool toCollect)
        {
            foreach (var prereq in prerequisiteTree)
                prereq.SetIsCollectedLocked(toCollect);
        }
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];
        public IEnumerable<ItemRequirement> ItemRequirements
        {
            get
            {
                foreach (var prereq in PrerequisiteTree)
                    foreach (var requirement in prereq.ItemRequirements)
                        yield return requirement;
            }
        }
        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return CollectionStatusType.ObtainedComplete;
                if (PrerequisiteTree.Any(p => p.CollectionStatus >= CollectionStatusType.Obtainable))
                    return CollectionStatusType.Obtainable;
                return CollectionStatusType.NotObtainable;
            }
        }

        public PrerequisiteOrNode(
            uint itemId,
            string itemName,
            List<IPrerequisiteNode>? prerequisiteTree,
            PrerequisiteNodeSourceType sourceType,
            string? nodeId = null
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            this.prerequisiteTree = prerequisiteTree ?? [];

            foreach (var prereq in PrerequisiteTree)
                prereq.OnPrerequisiteChange += handlePrereqChange;
        }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;

        private void handlePrereqChange() =>
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
            return PrerequisiteTree.Min(p => p.MinRemainingItems(newItemId) as int?) ?? 0;   // only need one, pick min needed
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

        public IPrerequisiteNode? AssignItemId(uint itemId)
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

        public List<uint> CollectLockItemIds()
        {
            return PrerequisiteTree
                .SelectMany(p => p.CollectLockItemIds())
                .ToList();
        }

        public HashSet<string> MeldableItemNames()
        {
            return PrerequisiteTree
                .SelectMany(p => p.MeldableItemNames())
                .ToHashSet();
        }
            

        public string GroupKey()
        {
            return $"""
                OR {ItemId} {IsCollected} {CollectLock} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            return $"([OR] [{PrerequisiteTree.Count}] {string.Join("\n    ", PrerequisiteTree.Select(p => p.ToString()))})";
        }
    }
}
