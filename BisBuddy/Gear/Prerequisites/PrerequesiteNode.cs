using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear.Prerequisites
{
    public delegate void PrerequisiteChangeHandler();

    public class PrerequisiteNode
    {
        private readonly List<PrerequisiteAndGroup> completePrerequisiteTree;
        private List<PrerequisiteAndGroup> activePrerequisiteTree;

        public string NodeId { get; init; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsMeldable { get; set; } = false;
        public IReadOnlyList<PrerequisiteAndGroup> ActivePrerequisiteTree
        {
            get => activePrerequisiteTree;
        }
        public IReadOnlyList<PrerequisiteAndGroup> CompletePrerequisiteTree
        {
            get => completePrerequisiteTree;
        }

        private bool collectLock = false;
        private bool isCollected = false;

        public bool IsCollected
        {
            get => isCollected;
            set
            {
                foreach (var group in CompletePrerequisiteTree)
                {
                    foreach (var prereq in group)
                        if (!prereq.CollectLock)
                            prereq.IsCollected = value;
                    group.ParentCollected = value;
                }

                if (isCollected == value)
                    return;

                if (CollectLock)
                    throw new InvalidOperationException($"Cannot {(value ? "collect" : "uncollect")} prereq {ItemId}, is locked.");

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

                if (ActivePrerequisiteTree.Count == 0)
                    return false;

                foreach (var group in ActivePrerequisiteTree)
                {
                    // one group entirely unlocked, possible to change IsCollected by only modifying this group
                    if (group.All(n => !n.CollectLock))
                        return false;
                }
                return true;
            }
            set
            {
                foreach (var prereq in CompletePrerequisiteNodes)
                {
                    prereq.CollectLock = value;
                }

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
            foreach (var group in CompletePrerequisiteTree)
            {
                group.ParentCollected = toCollect;
            }
            foreach (var prereq in CompletePrerequisiteNodes)
            {
                prereq.SetIsCollectedLocked(toCollect);
            }
            triggerPrerequisiteChange();
        }
        public HashSet<string> ChildNodeIds => [
            .. ActivePrerequisiteTree
                .SelectMany(p => p)
                .SelectMany(n => new HashSet<string>(n.ChildNodeIds) { n.NodeId })
        ];
        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return CollectionStatusType.ObtainedComplete;

                if (ActivePrerequisiteTree.Count == 0)
                    return CollectionStatusType.NotObtainable;

                var anyPartial = false;
                foreach (var group in ActivePrerequisiteTree)
                {
                    var groupStatus = group.CollectionStatus;
                    if (groupStatus >= CollectionStatusType.Obtainable)
                        return CollectionStatusType.Obtainable;
                    anyPartial |= groupStatus >= CollectionStatusType.NotObtainablePartial;
                }

                if (anyPartial)
                    return CollectionStatusType.NotObtainablePartial;
                return CollectionStatusType.NotObtainable;
            }
        }

        public PrerequisiteNode(
            uint itemId,
            string itemName,
            List<PrerequisiteAndGroup>? completePrerequisiteTree,
            bool isCollected = false,
            bool collectLock = false,
            bool isMeldable = false,
            string? nodeId = null
            )
        {
            NodeId = nodeId ?? Guid.NewGuid().ToString();
            ItemId = itemId;
            ItemName = itemName;
            IsMeldable = isMeldable;
            this.isCollected = isCollected;
            this.collectLock = collectLock;

            var newTree = completePrerequisiteTree ?? [];
            this.completePrerequisiteTree = newTree;
            this.activePrerequisiteTree = getActivePrerequisites();

            foreach (var group in CompletePrerequisiteTree)
            {
                group.ParentCollected = isCollected;
                foreach (var prereq in group)
                {
                    prereq.OnPrerequisiteChange += handlePrereqChange;
                }
            }
        }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;


        private List<PrerequisiteAndGroup> getActivePrerequisites() =>
            [.. completePrerequisiteTree.Where(g => g.IsActive)];

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
                foreach (var prereq in CompletePrerequisiteNodes)
                    foreach (var requirement in prereq.GetItemRequirements(includeDisabledNodes))
                        yield return requirement;
            }
            else
            {
                foreach (var prereq in ActivePrerequisiteNodes)
                    foreach (var requirement in prereq.GetItemRequirements(includeDisabledNodes))
                        yield return requirement;
            }
        }

        public IEnumerable<PrerequisiteNode> CompletePrerequisiteNodes
        {
            get
            {
                foreach (var group in completePrerequisiteTree)
                    foreach (var node in group)
                        yield return node;
            }
        }

        public IEnumerable<PrerequisiteNode> ActivePrerequisiteNodes
        {
            get
            {
                foreach (var group in activePrerequisiteTree)
                    foreach (var node in group)
                        yield return node;
            }
        }

        public void SetPrerequisiteGroupActiveStatus(int groupIdx, bool isActive)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(groupIdx, nameof(groupIdx));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(groupIdx, CompletePrerequisiteTree.Count, nameof(groupIdx));

            var group = CompletePrerequisiteTree[groupIdx];
            if (CompletePrerequisiteTree[groupIdx].IsActive == isActive)
                return;

            if (!isActive)
                activePrerequisiteTree.Remove(group);
            else
                activePrerequisiteTree.Add(group);

            group.IsActive = isActive;

            triggerPrerequisiteChange();
        }

        private void handlePrereqChange() =>
            triggerPrerequisiteChange();

        public void RemoveAllNodes()
        {
            foreach (var prereq in CompletePrerequisiteNodes)
            {
                prereq.OnPrerequisiteChange -= OnPrerequisiteChange;
            }
            completePrerequisiteTree.Clear();
            activePrerequisiteTree.Clear();
        }

        public void ReplaceGroup(int groupIdx, PrerequisiteAndGroup newGroup)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(groupIdx, nameof(groupIdx));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(groupIdx, completePrerequisiteTree.Count, nameof(groupIdx));

            var oldGroup = completePrerequisiteTree[groupIdx];

            foreach (var node in newGroup)
                node.OnPrerequisiteChange += handlePrereqChange;

            foreach (var node in oldGroup)
                node.OnPrerequisiteChange -= handlePrereqChange;

            completePrerequisiteTree[groupIdx] = newGroup;
            activePrerequisiteTree = getActivePrerequisites();
        }

        public void InsertGroup(int groupIdx, PrerequisiteAndGroup newGroup)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(groupIdx, nameof(groupIdx));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(groupIdx, completePrerequisiteTree.Count, nameof(groupIdx));

            foreach (var node in newGroup)
                node.OnPrerequisiteChange += handlePrereqChange;

            completePrerequisiteTree.Insert(groupIdx, newGroup);
            activePrerequisiteTree = getActivePrerequisites();
        }

        public void AddGroup(PrerequisiteAndGroup newGroup) =>
            InsertGroup(completePrerequisiteTree.Count, newGroup);

        public int MinRemainingItems(uint? newItemId = null)
        {
            var remainingItems = 0;

            if (IsCollected)
                return 0;

            if (newItemId == ItemId)
                return 0;

            if (ActivePrerequisiteTree.Count == 0)
                return 1;

            foreach (var group in ActivePrerequisiteTree)
            {
                var itemAvailable = newItemId != null;
                var groupRemainingItems = 0;
                foreach (var prereq in group)
                {
                    // calculate how many items remaining with no item provided
                    var minNoItem = prereq.MinRemainingItems();
                    if (!itemAvailable)
                    {
                        groupRemainingItems += minNoItem;
                        continue;
                    }

                    // calculate how many items remaining with provided item
                    var minWithItem = prereq.MinRemainingItems(newItemId);
                    if (minNoItem != minWithItem)
                        // if not equal, then must be able to use this item for this prereq
                        // and no longer be able to be used for future prereqs in node
                        itemAvailable = false;

                    groupRemainingItems += minWithItem;
                }
                remainingItems = Math.Min(groupRemainingItems, remainingItems);
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

            foreach (var prereq in CompletePrerequisiteNodes)
                prereq.AddNeededItemIds(neededCounts, startDepth + 1);
        }

        public int PrerequisiteCount()
        {
            return 1 + ActivePrerequisiteTree.SelectMany(g => g).Sum(p => p.PrerequisiteCount());
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

            foreach (var prereq in CompletePrerequisiteNodes)
            {
                var assignResult = prereq.AssignItemId(itemId);
                if (assignResult is not null)
                    return assignResult;
            }

            return null;
        }

        public List<uint> CollectLockItemIds()
        {
            if (CollectLock)
                return [ItemId];

            return ActivePrerequisiteTree
                .SelectMany(g => g)
                .SelectMany(p => p.CollectLockItemIds())
                .ToList();
        }

        public HashSet<string> MeldableItemNames()
        {
            var prereqNames = ActivePrerequisiteTree
                .SelectMany(g => g)
                .SelectMany(p => p.MeldableItemNames())
                .ToHashSet();

            if (IsMeldable)
                prereqNames.Add(ItemName);

            return prereqNames;
        }


        public override string ToString()
        {
            var childrenStr = "";
            if (CompletePrerequisiteTree.Count > 0)
            {
                childrenStr = (
                    $" [{CompletePrerequisiteTree.Count}] =>\n  OR " +
                    string.Join(
                        "\n  OR ",
                        CompletePrerequisiteTree.Select(g => g.ToString().Replace("\n", "\n  "))
                    )
                );
            }
            var collectLockStr = CollectLock ? " (L)" : "";
            return $"[{ItemName} ({ItemId})] [{Enum.GetName(CollectionStatus)}{collectLockStr}] [{(IsCollected ? "C" : "U")}] [{NodeId[..10]}..]{childrenStr}";
        }

        public PrerequisiteNode Clone() => new(
            itemId: ItemId,
            itemName: ItemName,
            completePrerequisiteTree: [.. completePrerequisiteTree.Select(g => g.Clone())],
            isCollected: isCollected,
            collectLock: collectLock,
            isMeldable: IsMeldable
        );
    }
}
