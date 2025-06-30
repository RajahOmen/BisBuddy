using Dalamud.Game.Inventory;
using System.Collections.Generic;
using System.Numerics;
namespace BisBuddy.Util
{
    public static class Constants
    {
        public static readonly string PluginName = "BISBuddy";
        public static readonly int MaxGearsetCount = 25;

        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';

        public static readonly Vector3 CustomNodeMultiplyColor = new(0.393f, 0.393f, 0.393f);
        public static readonly float BrightListItemAlpha = 1.0f;

        public static readonly IReadOnlyList<GameInventoryType> InventorySources = [
            GameInventoryType.Inventory1,
            GameInventoryType.Inventory2,
            GameInventoryType.Inventory3,
            GameInventoryType.Inventory4,
            GameInventoryType.EquippedItems,
            GameInventoryType.ArmoryMainHand,
            GameInventoryType.ArmoryOffHand,
            GameInventoryType.ArmoryHead,
            GameInventoryType.ArmoryBody,
            GameInventoryType.ArmoryHands,
            GameInventoryType.ArmoryLegs,
            GameInventoryType.ArmoryFeets,
            GameInventoryType.ArmoryEar,
            GameInventoryType.ArmoryNeck,
            GameInventoryType.ArmoryWrist,
            GameInventoryType.ArmoryRings,
            ];
    }
}
