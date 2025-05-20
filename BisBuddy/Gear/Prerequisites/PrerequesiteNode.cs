using System.Collections.Generic;

namespace BisBuddy.Gear.Prerequisites
{
    public interface IPrerequisiteNode
    {
        public string NodeId { get; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsCollected { get; }
        public bool IsManuallyCollected { get; }
        public bool IsObtainable { get; }
        public List<IPrerequisiteNode> PrerequisiteTree { get; set; }
        public HashSet<string> ChildNodeIds { get; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public void SetCollected(bool collected, bool manualToggle);
        public int MinRemainingItems(uint? newItemId = null);
        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0);
        public IPrerequisiteNode? AssignItemId(uint itemId);
        public List<uint> ManuallyCollectedItemIds();
        public int PrerequisiteCount();
        public IEnumerable<ItemRequirement> ItemRequirements(Gearset parentGearset, Gearpiece parentGearpiece);
        public HashSet<string> MeldableItemNames();
        public string GroupKey();
    }
}
