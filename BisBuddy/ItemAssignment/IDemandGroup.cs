using Dalamud.Game.Inventory;

namespace BisBuddy.ItemAssignment
{
    public interface IDemandGroup
    {
        public DemandGroupType Type { get; }
        public uint ItemId { get; }
        public int CandidateEdgeWeight(GameInventoryItem candidate);
    }
}
