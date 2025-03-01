using BisBuddy.Gear.Prerequisites;
using Dalamud.Game.Inventory;

namespace BisBuddy.ItemAssignment
{
    public class PrerequisitesAssignment(GameInventoryItem? item, PrerequisiteNode prerequisiteGroup)
    {
        // null if prerequisites unassigned
        public GameInventoryItem? Item { get; set; } = item;
        public PrerequisiteNode PrerequisiteGroup { get; set; } = prerequisiteGroup;
    }
}
