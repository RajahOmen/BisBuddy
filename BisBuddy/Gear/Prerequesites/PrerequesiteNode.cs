using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear.Prerequesites
{
    [JsonDerivedType(typeof(PrerequesiteAtomNode), typeDiscriminator: "atom")]
    [JsonDerivedType(typeof(PrerequesiteAndNode), typeDiscriminator: "and")]
    [JsonDerivedType(typeof(PrerequesiteOrNode), typeDiscriminator: "or")]
    public interface PrerequesiteNode
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public bool IsCollected { get; }
        public bool IsManuallyCollected { get; }
        public bool IsObtainable { get; }
        public List<PrerequesiteNode> PrerequesiteTree { get; set; }
        public PrerequesiteNodeSourceType SourceType { get; set; }

        public void SetCollected(bool collected, bool manualToggle);
        public int ItemNeededCount(uint itemId, bool ignoreCollected);
        public int MinRemainingItems();
        public void AddNeededItemIds(Dictionary<uint, int> neededCounts);
        public int PrerequesiteCount();
        public string GroupKey();
    }
}
