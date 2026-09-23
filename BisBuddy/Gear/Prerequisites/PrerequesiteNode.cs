using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    public delegate void PrerequisiteChangeHandler();

    public interface IPrerequisiteNode : ICollectableItem
    {
        public string NodeId { get; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public IReadOnlyList<PrerequisiteNode> PrerequisiteTree { get; }
        public HashSet<string> ChildNodeIds { get; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;
        public IEnumerable<ItemRequirement> GetItemRequirements(bool includeDisabledNodes = false);
        public void AddNode(PrerequisiteNode newNode);
        public void ReplaceNode(int index, PrerequisiteNode newNode);
        public void InsertNode(int index, PrerequisiteNode newNode);
        public int MinRemainingItems(uint? newItemId = null);
        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0);
        public PrerequisiteNode? AssignItemId(uint itemId);
        public List<uint> CollectLockItemIds();
        public int PrerequisiteCount();
        public HashSet<string> MeldableItemNames();
        public string GroupKey();
    }

    public class PrerequisiteNode
    {
        private readonly List<(PrerequisiteNode Node, bool IsActive)> completePrerequisiteTree;
        private List<PrerequisiteNode> activePrerequisiteTree;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsMeldable { get; set; } = false;
        public ChildGroupType GroupType {
            get => completePrerequisiteTree.Count > 0 ? field : ChildGroupType.Unit;
            set;
        }
        public PrerequisiteNodeSourceType SourceType { get; set; }
        public IReadOnlyList<PrerequisiteNode> PrerequisiteTree
        {
            get => activePrerequisiteTree;
        }
        public IReadOnlyList<(PrerequisiteNode Node, bool IsActive)> CompletePrerequisiteTree
        {
            get => completePrerequisiteTree;
        }

        private bool collectLock = false;
        private bool isCollected = false;

        public bool IsCollected
        {
            get => isCollected || GroupType switch
            {
                ChildGroupType.Or => PrerequisiteTree.Any(p => p.IsCollected),
                ChildGroupType.And => PrerequisiteTree.All(p => p.IsCollected),
                _ => false,
            };
            set
            {
                foreach (var (prereq, _) in completePrerequisiteTree)
                    if (!prereq.CollectLock)
                        prereq.IsCollected = value;

                if (CollectLock)
                    throw new InvalidOperationException($"Cannot {(value ? "collect" : "uncollect")} {Enum.GetName(GroupType)} prereq {ItemId}, is locked.");

                isCollected = value;

                triggerPrerequisiteChange();
            }
        }

        private void triggerPrerequisiteChange() =>
            OnPrerequisiteChange?.Invoke();

        public bool CollectLock
        {
            get 
            {
                if (collectLock)
                    return true;

                switch (GroupType)
                {
                    case ChildGroupType.Or:
                        foreach (var p in PrerequisiteTree)
                        {
                            if (!p.CollectLock)
                                return false;
                            if (p.IsCollected)
                                return true;
                        }
                        return true;
                    case ChildGroupType.And:
                    default:
                        return PrerequisiteTree.Any(p => p.CollectLock);
                }
            }
            set
            {
                foreach (var (prereq, _) in completePrerequisiteTree)
                    prereq.CollectLock = value;

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
        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return CollectionStatusType.ObtainedComplete;

                if (PrerequisiteTree.Count == 0)
                    return CollectionStatusType.NotObtainable;

                var (obtainable, anyPartial) = GroupType switch
                {
                    ChildGroupType.Or => PrerequisiteTree
                        .Select(p => p.CollectionStatus)
                        .Aggregate((AnyObtainable: false, AnyPartial: false), (acc, status) => (
                            acc.AnyObtainable || status >= CollectionStatusType.Obtainable,
                            acc.AnyPartial || status >= CollectionStatusType.NotObtainablePartial
                        )),
                    ChildGroupType.And => PrerequisiteTree
                        .Select(p => p.CollectionStatus)
                        .Aggregate((AllObtainable: true, AnyPartial: false), (acc, status) => (
                            acc.AllObtainable && status >= CollectionStatusType.Obtainable,
                            acc.AnyPartial || status >= CollectionStatusType.NotObtainablePartial
                        )),
                    _ => (false, false)
                };

                if (obtainable)
                    return CollectionStatusType.Obtainable;
                if (anyPartial)
                    return CollectionStatusType.NotObtainablePartial;
                return CollectionStatusType.NotObtainable;
            }
        }

        public PrerequisiteNode(
            uint itemId,
            string itemName,
            List<PrerequisiteNode>? prerequisiteTree,
            ChildGroupType groupType,
            PrerequisiteNodeSourceType sourceType,
            bool isCollected = false,
            bool collectLock = false,
            bool isMeldable = false,
            string? nodeId = null,
            bool isActive = true,
            List<int>? disabledPrereqs = null
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            SourceType = sourceType;
            IsMeldable = isMeldable;
            this.isCollected = isCollected;
            this.collectLock = collectLock;

            var newTree = prerequisiteTree ?? [];
            var newDisabled = disabledPrereqs ?? [];
            this.completePrerequisiteTree = newTree
                .Select((node, idx) => (node, !newDisabled.Contains(idx)))
                .ToList();
            this.activePrerequisiteTree = completePrerequisiteTree
                .Where(entry => entry.IsActive)
                .Select(entry => entry.Node)
                .ToList();
            GroupType = groupType;

            foreach (var (prereq, _) in this.completePrerequisiteTree)
                prereq.OnPrerequisiteChange += handlePrereqChange;
        }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;

        public IEnumerable<ItemRequirement> GetItemRequirements(bool includeDisabledNodes = false)
        {
            yield return new ItemRequirement()
            {
                ItemId = ItemId,
                CollectionStatus = CollectionStatus,
                RequirementType = RequirementType.Prerequisite,
            };

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

        public void SetPrerequisiteActiveStatus(PrerequisiteNode prereq, bool isActive)
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

            triggerPrerequisiteChange();
        }

        private void handlePrereqChange() =>
            triggerPrerequisiteChange();

        public void AddNode(PrerequisiteNode node) =>
            InsertNode(PrerequisiteTree.Count, node);

        public void RemoveAllNodes()
        {
            foreach (var (node, isActive) in completePrerequisiteTree)
            {
                node.OnPrerequisiteChange -= OnPrerequisiteChange;
            }
            completePrerequisiteTree.Clear();
            activePrerequisiteTree.Clear();
        }

        public void ReplaceNode(int index, PrerequisiteNode newNode)
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

        public void InsertNode(int index, PrerequisiteNode node)
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
            var remainingItems = 0;

            if (IsCollected)
                return 0;

            if (newItemId == ItemId)
                return 0;

            if (PrerequisiteTree.Count == 0)
                return 1;

            switch (GroupType)
            {
                case ChildGroupType.Or:
                    // only need one, pick min needed
                    remainingItems = PrerequisiteTree.Min(p => p.MinRemainingItems(newItemId) as int?) ?? 0;
                    break;
                case ChildGroupType.And:
                    // is new item still available to use for this loop
                    var itemAvailable = newItemId != null;
                    foreach (var prereq in PrerequisiteTree)
                    {
                        // calculate how many items remaining with no item provided
                        var minNoItem = prereq.MinRemainingItems();
                        if (!itemAvailable)
                        {
                            remainingItems += minNoItem;
                            continue;
                        }

                        // calculate how many items remaining with provided item
                        var minWithItem = prereq.MinRemainingItems(newItemId);
                        if (minNoItem != minWithItem)
                            // if not equal, then must be able to use this item for this prereq
                            // and no longer be able to be used for future prereqs in node
                            itemAvailable = false;

                        remainingItems += minWithItem;
                    }
                    break;
            }
            return remainingItems;
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
                IsCollected = true;
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


        public List<(PrerequisiteNode Node, int Count)> Groups()
        {
            return PrerequisiteTree
                .GroupBy(p => p.GroupKey())
                .Select(g => (g.First(), g.Count()))
                .OrderBy(g => g.Item1.CollectionStatus)
                .ToList();
        }


        public string GroupKey()
        {
            return $"""
                {Enum.GetName(GroupType)} {ItemId} {IsCollected} {CollectLock} {SourceType}
                {string.Join(" ", PrerequisiteTree.Select(p => p.GroupKey()))}
                """;
        }

        public override string ToString()
        {
            var childrenStr = PrerequisiteTree.Count > 0
                ? (
                    $" [{PrerequisiteTree.Count}] =>\n    " +
                    string.Join(
                        "\n    ",
                        CompletePrerequisiteTree.Select(p =>
                            $"{p.Node.ToString().Replace("\n", "\n    ")}" +
                            $"{(p.IsActive ? "" : "[Inactive]")}"
                        )
                    )
                ) : "";
            var collectLockStr = CollectLock ? " (L)" : "";
            return $"[{ItemName} ({ItemId})] [{Enum.GetName(GroupType)}] [{Enum.GetName(SourceType)}] [{Enum.GetName(CollectionStatus)}{collectLockStr}] [{NodeId[..10]}..]{childrenStr}";
        }
    }
}
