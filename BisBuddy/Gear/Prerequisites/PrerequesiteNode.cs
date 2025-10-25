using System.Collections.Generic;

namespace BisBuddy.Gear.Prerequisites
{
    public delegate void PrerequisiteChangeHandler();

    public interface IPrerequisiteNode : ICollectableItem
    {
        public string NodeId { get; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public IReadOnlyList<IPrerequisiteNode> PrerequisiteTree { get; }
        public HashSet<string> ChildNodeIds { get; }
        public IEnumerable<ItemRequirement> ItemRequirements { get; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;
        public void AddNode(IPrerequisiteNode newNode);
        public void ReplaceNode(int index, IPrerequisiteNode newNode);
        public void InsertNode(int index, IPrerequisiteNode newNode);
        public int MinRemainingItems(uint? newItemId = null);
        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0);
        public IPrerequisiteNode? AssignItemId(uint itemId);
        public List<uint> CollectLockItemIds();
        public int PrerequisiteCount();
        public HashSet<string> MeldableItemNames();
        public string GroupKey();
    }
}
