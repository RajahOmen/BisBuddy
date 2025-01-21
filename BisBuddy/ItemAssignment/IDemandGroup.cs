using Dalamud.Game.Inventory;

namespace BisBuddy.ItemAssignment
{
    internal interface IDemandGroup
    {
        public DemandGroupType Type { get; }
        public uint ItemId { get; }
        public int CandidateEdgeWeight(GameInventoryItem candidate);
    }
}
