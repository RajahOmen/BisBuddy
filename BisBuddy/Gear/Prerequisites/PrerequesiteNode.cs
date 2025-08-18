using System.Collections.Generic;

namespace BisBuddy.Gear.Prerequisites
{
    public delegate void PrerequisiteChangeHandler();

    public interface IPrerequisiteNode
    {
        public string NodeId { get; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsCollected { get; }
        public bool IsManuallyCollected { get; }
        public bool IsObtainable { get; }
        public IReadOnlyList<IPrerequisiteNode> PrerequisiteTree { get; set; }
        public HashSet<string> ChildNodeIds { get; }
        public IEnumerable<ItemRequirement> ItemRequirements { get; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public event PrerequisiteChangeHandler? OnPrerequisiteChange;
        public void AddNode(IPrerequisiteNode newNode);
        public void ReplaceNode(int index, IPrerequisiteNode newNode);
        public void InsertNode(int index, IPrerequisiteNode newNode);
        public void SetCollected(bool collected, bool manualToggle);
        public int MinRemainingItems(uint? newItemId = null);
        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0);
        public IPrerequisiteNode? AssignItemId(uint itemId);
        public List<uint> ManuallyCollectedItemIds();
        public int PrerequisiteCount();
        public HashSet<string> MeldableItemNames();
        public string GroupKey();
    }
}
