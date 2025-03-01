using BisBuddy.Gear;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public interface IAssignmentGroup
    {
        public AssignmentGroupType Type { get; }
        public uint ItemId { get; }
        public int CandidateEdgeWeight(uint candidateId, List<Materia> candidateMateria);
    }
}
