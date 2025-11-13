using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    public class PrerequisiteOrNode : IPrerequisiteNode
    {
        private readonly List<(IPrerequisiteNode Node, bool IsActive)> completePrerequisiteTree;
        private List<IPrerequisiteNode> activePrerequisiteTree;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public IReadOnlyList<IPrerequisiteNode> PrerequisiteTree
        {
            get => activePrerequisiteTree;
        }
        public IReadOnlyList<(IPrerequisiteNode Node, bool IsActive)> CompletePrerequisiteTree
        {
            get => completePrerequisiteTree;
        }

        public bool IsCollected
        {
            get => PrerequisiteTree.Any(p => p.IsCollected);
            set
            {
                foreach (var (prereq, _) in completePrerequisiteTree)
                    if (!prereq.CollectLock)
                        prereq.IsCollected = value;
            }
        }
        public bool CollectLock
        {
            get => PrerequisiteTree.All(p => p.CollectLock);
            set
            {
                foreach (var (prereq, _) in completePrerequisiteTree)
                    prereq.CollectLock = value;
            }
        }
        public void SetIsCollectedLocked(bool toCollect)
        {
            foreach (var (prereq, _) in completePrerequisiteTree)
                prereq.SetIsCollectedLocked(toCollect);
        }
        public HashSet<string> ChildNodeIds => [.. PrerequisiteTree.Select(p => p.NodeId), .. PrerequisiteTree.SelectMany(p => p.ChildNodeIds)];
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
            string? nodeId = null,
            bool isActive = true,
            List<int>? disabledPrereqs = null
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;

            var newTree = prerequisiteTree ?? [];
            var newDisabled = disabledPrereqs ?? [];
            this.completePrerequisiteTree = newTree
                .Select((node, idx) => (node, !newDisabled.Contains(idx)))
                .ToList();
            this.activePrerequisiteTree = completePrerequisiteTree
                .Where(entry => entry.IsActive)
                .Select(entry => entry.Node)
                .ToList();

            foreach (var (prereq, _) in this.completePrerequisiteTree)
                prereq.OnPrerequisiteChange += handlePrereqChange;
        }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;

        public IEnumerable<ItemRequirement> GetItemRequirements(bool includeDisabledNodes = false)
        {
            if (includeDisabledNodes)
            {
                foreach (var (prereq, _) in CompletePrerequisiteTree)
                    foreach (var requirement in prereq.GetItemRequirements(includeDisabledNodes))
                        yield return requirement;
            }
            else
            {
                foreach (var prereq in PrerequisiteTree)
                    foreach (var requirement in prereq.GetItemRequirements(includeDisabledNodes))
                        yield return requirement;
            }
        }

        public void SetPrerequisiteActiveStatus(IPrerequisiteNode prereq, bool isActive)
        {
            var matches = completePrerequisiteTree.Index().Where(entry => entry.Item.Node == prereq);
            if (!matches.Any())
                throw new ArgumentException("Prerequisite node not found in this OR group", prereq.ItemName);

            if (matches.Count() > 1)
                throw new InvalidOperationException($"Multiple matching prerequisite nodes found in this OR group (\"{prereq.ItemName}\")");

            var (idx, completeNode) = matches.First();
            var oldIsActive = completeNode.IsActive;
            if (isActive == oldIsActive)
                return;

            completePrerequisiteTree[idx] = (completeNode.Node, isActive);
            activePrerequisiteTree = completePrerequisiteTree
                .Where(entry => entry.IsActive)
                .Select(entry => entry.Node)
                .ToList();

            OnPrerequisiteChange?.Invoke();
        }

        private void handlePrereqChange() =>
            OnPrerequisiteChange?.Invoke();

        public void AddNode(IPrerequisiteNode node) =>
            InsertNode(PrerequisiteTree.Count, node);

        public void ReplaceNode(int index, IPrerequisiteNode newNode)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, PrerequisiteTree.Count);

            var oldNode = PrerequisiteTree[index];

            var matches = completePrerequisiteTree.Index().Where(entry => entry.Item.Node == oldNode);
            if (!matches.Any() || matches.Count() > 1)
                throw new InvalidOperationException($"Invalid match count {matches.Count()} for replacing node");

            var (completeIdx, completeNode) = matches.First();

            activePrerequisiteTree[index] = newNode;

            completePrerequisiteTree.Add((newNode, completeNode.IsActive));
            completePrerequisiteTree.Remove((oldNode, completeNode.IsActive));

            oldNode.OnPrerequisiteChange -= handlePrereqChange;
            newNode.OnPrerequisiteChange += handlePrereqChange;
        }

        public void InsertNode(int index, IPrerequisiteNode node)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, PrerequisiteTree.Count);

            int completeIdx;
            if (index < PrerequisiteTree.Count)
            {
                var prevNodeAtIdx = activePrerequisiteTree[index];
                completeIdx = completePrerequisiteTree
                    .Index()
                    .Where(entry => entry.Item.Node == prevNodeAtIdx)
                    .Select(entry => entry.Index)
                    .First();
            }
            else
            {
                completeIdx = completePrerequisiteTree.Count;
            }


            activePrerequisiteTree.Insert(index, node);
            completePrerequisiteTree.Insert(completeIdx, (node, true));

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
