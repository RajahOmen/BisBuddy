using BisBuddy.Gear;
using Dalamud.Game.Inventory;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    internal class GearpiecesAssignment
    {
        // null if gearpieces unassigned
        public GameInventoryItem? Item { get; set; }
        public List<Gearpiece> Gearpieces { get; set; } = [];
    }
}
