using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear.Prerequisites
{
    [JsonDerivedType(typeof(PrerequisiteAtomNode), typeDiscriminator: "atom")]
    [JsonDerivedType(typeof(PrerequisiteAndNode), typeDiscriminator: "and")]
    [JsonDerivedType(typeof(PrerequisiteOrNode), typeDiscriminator: "or")]
    public interface PrerequisiteNode
    {
        public string NodeId { get; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsCollected { get; }
        public bool IsManuallyCollected { get; }
        public bool IsObtainable { get; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }
        public HashSet<string> ChildNodeIds { get; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public void SetCollected(bool collected, bool manualToggle);
        public int ItemNeededCount(uint itemId, bool ignoreCollected);
        public int MinRemainingItems(uint? newItemId = null);
        public void AddNeededItemIds(Dictionary<uint, (int MinDepth, int Count)> neededCounts, int startDepth = 0);
        public PrerequisiteNode? AssignItemId(uint itemId);
        public List<uint> ManuallyCollectedItemIds();
        public int PrerequisiteCount();
        public string GroupKey();
    }
}
