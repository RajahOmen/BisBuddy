using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear.Prerequisites
{
    [JsonDerivedType(typeof(PrerequisiteAtomNode), typeDiscriminator: "atom")]
    [JsonDerivedType(typeof(PrerequisiteAndNode), typeDiscriminator: "and")]
    [JsonDerivedType(typeof(PrerequisiteOrNode), typeDiscriminator: "or")]
    public interface PrerequisiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsCollected { get; }
        public bool IsManuallyCollected { get; }
        public bool IsObtainable { get; }
        public List<PrerequisiteNode> PrerequisiteTree { get; set; }
        public PrerequisiteNodeSourceType SourceType { get; set; }

        public void SetCollected(bool collected, bool manualToggle);
        public int ItemNeededCount(uint itemId, bool ignoreCollected);
        public int MinRemainingItems();
        public void AddNeededItemIds(Dictionary<uint, int> neededCounts);
        public int PrerequisiteCount();
        public string GroupKey();
    }
}
