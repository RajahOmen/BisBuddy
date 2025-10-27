using Dalamud.Game.Inventory;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public struct SolveResult
    {
        public int[] Assignments;
        public int[,] Edges;
        public List<GameInventoryItem> CandidateItems;
        public List<IAssignmentGroup> AssignmentGroups;

        public readonly void Deconstruct(
            out int[] assignments,
            out int[,] edges,
            out List<GameInventoryItem> candidateItems,
            out List<IAssignmentGroup> assignmentGroups
            )
        {
            assignments = Assignments;
            edges = Edges;
            candidateItems = CandidateItems;
            assignmentGroups = AssignmentGroups;
        }
    }
}
