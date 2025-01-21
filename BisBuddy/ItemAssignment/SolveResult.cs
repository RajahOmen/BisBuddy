using BisBuddy.Gear;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    internal class SolveResult
    {
        public List<GearpiecesAssignment> GearpiecesAssignments { get; set; } = [];
        public List<PrerequesitesAssignment> PrerequesitesAssignments { get; set; } = [];

        public void AddAssignment(GameInventoryItem? item, IDemandGroup groupAssigned)
        {
            switch (groupAssigned.Type)
            {
                case DemandGroupType.Gearpiece:
                    var gearpiecesAssignments = new GearpiecesAssignment
                    {
                        Item = item,
                        Gearpieces = ((GearpieceGroup)groupAssigned).Gearpieces,
                    };
                    GearpiecesAssignments.Add(gearpiecesAssignments);
                    break;
                case DemandGroupType.Prerequesite:
                    var prereqGroupAssigned = (PrerequesiteGroup)groupAssigned;
                    AddPrerequesiteAssignments(item, prereqGroupAssigned.GearpiecePrerequesites);
                    break;
                default:
                    throw new Exception($"Unknown demand group type {groupAssigned.Type}");
            }
        }

        private void AddPrerequesiteAssignments(GameInventoryItem? item, List<GearpiecePrerequesite> groupPrereqs)
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
                    AddPrerequesiteAssignments(item, groupPrereq.Prerequesites);
                }
            }
        }
    }
}
