using BisBuddy.Gear.Prerequesites;
using Dalamud.Game.Inventory;

namespace BisBuddy.ItemAssignment
{
    public class PrerequesitesAssignment(GameInventoryItem? item, PrerequesiteNode prerequesiteGroup)
    {
        // null if prerequesites unassigned
        public GameInventoryItem? Item { get; set; } = item;
        public PrerequesiteNode PrerequesiteGroup { get; set; } = prerequesiteGroup;
    }
}
