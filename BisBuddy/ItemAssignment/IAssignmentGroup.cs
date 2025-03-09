using BisBuddy.Gear;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public interface IAssignmentGroup
    {
        public AssignmentGroupType Type { get; }
        public uint ItemId { get; }
        public bool NeedsItemId(uint candidateItemId);
        public bool AddMatchingGearpiece(Gearpiece gearpiece, Gearset gearset);
        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria);
    }
}
