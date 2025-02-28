using BisBuddy.Gear.Prerequesites;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public class SolveResult
    {
        public List<GearpiecesAssignment> GearpiecesAssignments { get; set; } = [];
        public List<PrerequesitesAssignment> PrerequesitesAssignments { get; set; } = [];

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
                case AssignmentGroupType.Prerequesite:
                    var prereqGroupAssigned = (PrerequesiteAssignmentGroup)groupAssigned;
                    AddPrerequesiteAssignments(item, prereqGroupAssigned.PrerequesiteGroups);
                    break;
                default:
                    throw new Exception($"Unknown demand group type {groupAssigned.Type}");
            }
        }

        private void AddPrerequesiteAssignments(GameInventoryItem? item, List<PrerequesiteNode> groupPrereqs)
        {
            var itemId = item?.ItemId ?? 0;

            foreach (var groupPrereq in groupPrereqs)
            {
                if (groupPrereq.ItemId == itemId)
                { // this is the item, "assign" & move on (children are automatically assigned)
                    var prereqAssignment = new PrerequesitesAssignment(
                        item,
                        groupPrereq
                    );
                    PrerequesitesAssignments.Add(prereqAssignment);
                }
                else
                { // this isn't the item. Don't assign, and recurse on children
                    var prereqAssignment = new PrerequesitesAssignment(
                        null,
                        groupPrereq
                    );
                    PrerequesitesAssignments.Add(prereqAssignment);
                    AddPrerequesiteAssignments(item, groupPrereq.PrerequesiteTree);
                }
            }
        }
    }
}
