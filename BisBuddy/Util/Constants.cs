using Dalamud.Game.Inventory;
using System.Collections.Generic;
using System.Numerics;
namespace BisBuddy.Util
{
    public static class Constants
    {
        public static readonly string PluginName = "BISBuddy";
        public static readonly string FullChatCommand = "/bisbuddy";
        public static readonly string ShortChatCommand = "/bis";
        public static readonly int MaxGearsetCount = 25;
        public static readonly int DefaultGearsetPriority = 0;

        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';

        /// <summary>
        /// The name of the directory under the plugin config directory where gearsets are saved
        /// </summary>
        public static readonly string GearsetsDirectoryName = "gearsets";

        /// <summary>
        /// The id of the icon corresponding to the icon directly preceding the icons for the classes and jobs
        /// When given a ClassJob rowId, the IconId matching the ClassJob is ClassJobIconIdOffset + IconId
        /// </summary>
        public static readonly int ClassJobIconIdOffsetFramed = 62100;
        public static readonly int ClassJobIconIdOffset = 62000;
        public static readonly int CompanionIconOffset = 2;

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

        public static readonly Vector2 SelectableListSpacing = new(5, 5);
    }
}
