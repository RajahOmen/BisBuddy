using BisBuddy.Gear;
using BisBuddy.Gear.Melds;

namespace BisBuddy.ItemAssignment
{
    public interface IAssignmentGroup
    {
        public AssignmentGroupType Type { get; }
        public uint ItemId { get; }
        public bool NeedsItemId(uint candidateItemId);
        public bool AddMatchingGearpiece(Gearpiece gearpiece, Gearset gearset);
        public int CandidateEdgeWeight(uint candidateId, MateriaGroup candidateMateria);
    }
}
