using BisBuddy.Gear.Prerequisites;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public class SolveResult
    {
        public List<GearpiecesAssignment> GearpiecesAssignments { get; set; } = [];
        public List<PrerequisitesAssignment> PrerequisitesAssignments { get; set; } = [];

        public void AddAssignment(GameInventoryItem? item, IAssignmentGroup groupAssigned)
        {
            switch (groupAssigned.Type)
            {
                case AssignmentGroupType.Gearpiece:
                    var gearpiecesAssignments = new GearpiecesAssignment
                    {
                        Item = item,
                        Gearpieces = ((GearpieceAssignmentGroup)groupAssigned).Gearpieces,
                    };
                    GearpiecesAssignments.Add(gearpiecesAssignments);
                    break;
                case AssignmentGroupType.Prerequisite:
                    var prereqGroupAssigned = (PrerequisiteAssignmentGroup)groupAssigned;
                    AddPrerequisiteAssignments(item, prereqGroupAssigned.PrerequisiteGroups);
                    break;
                default:
                    throw new Exception($"Unknown demand group type {groupAssigned.Type}");
            }
        }

        private void AddPrerequisiteAssignments(GameInventoryItem? item, List<PrerequisiteNode> groupPrereqs)
        {
            var itemId = item?.ItemId ?? 0;

            foreach (var groupPrereq in groupPrereqs)
            {
                if (groupPrereq.ItemId == itemId)
                { // this is the item, "assign" & move on (children are automatically assigned)
                    var prereqAssignment = new PrerequisitesAssignment(
                        item,
                        groupPrereq
                    );
                    PrerequisitesAssignments.Add(prereqAssignment);
                }
                else
                { // this isn't the item. Don't assign, and recurse on children
                    var prereqAssignment = new PrerequisitesAssignment(
                        null,
                        groupPrereq
                    );
                    PrerequisitesAssignments.Add(prereqAssignment);
                    AddPrerequisiteAssignments(item, groupPrereq.PrerequisiteTree);
                }
            }
        }
    }
}
