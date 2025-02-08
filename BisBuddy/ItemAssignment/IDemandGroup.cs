using BisBuddy.Gear;
using Dalamud.Game.Inventory;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public interface IDemandGroup
    {
        public DemandGroupType Type { get; }
        public uint ItemId { get; }
        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria);
    }
}
