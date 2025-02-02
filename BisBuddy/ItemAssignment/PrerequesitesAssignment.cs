using BisBuddy.Gear;
using Dalamud.Game.Inventory;

namespace BisBuddy.ItemAssignment
{
    public class PrerequesitesAssignment(GameInventoryItem? item, GearpiecePrerequesite gearpiecePrerequesite)
    {
        // null if prerequesites unassigned
        public GameInventoryItem? Item { get; set; } = item;
        public GearpiecePrerequesite GearpiecePrerequesite { get; set; } = gearpiecePrerequesite;
    }
}
