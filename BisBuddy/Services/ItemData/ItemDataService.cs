using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Services;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GearMateria = BisBuddy.Gear.Materia;
using SheetMateria = Lumina.Excel.Sheets.Materia;

namespace BisBuddy.Items
{
    public partial class ItemDataService : IItemDataService
    {
        private readonly ITypedLogger<ItemDataService> logger;
        private readonly IDataManager dataManager;
        private readonly IGameInventory gameInventory;

        private ILookup<uint, uint>? itemsCoffers = null;
        private ILookup<uint, List<uint>>? itemsPrerequisites = null;

        public static readonly int MaxItemPrerequisites = 25;
        private ExcelSheet<Item> ItemSheet { get; init; }
        private ExcelSheet<Item> ItemSheetEn { get; init; }
        private ExcelSheet<SpecialShop> ShopSheet { get; init; }
        private ExcelSheet<SheetMateria> Materia { get; init; }
        public ILookup<uint, uint> ItemsCoffers
        {
            get
            {
                if (itemsCoffers == null)
                {
                    itemsCoffers = generateItemsCoffers(ItemSheetEn);
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        itemsCoffers = null;
                    });
                }
                return itemsCoffers;
            }
        }
        public ILookup<uint, List<uint>> ItemsPrerequisites
        {
            get
            {
                if (itemsPrerequisites == null)
                {
                    itemsPrerequisites = generateItemsPrerequisites();
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        itemsPrerequisites = null;
                    });
                }
                return itemsPrerequisites;
            }
        }
        private Dictionary<string, uint> NameToId { get; init; }
        private Dictionary<string, (uint statId, string statName, int statLevel, int statQuantity)> MateriaNameToStat { get; init; } = [];
        private Dictionary<(uint materiaId, int materiaGrade), uint> materiaItemIds { get; init; }

        public ItemDataService(
            ITypedLogger<ItemDataService> logger,
            IDataManager dataManager,
            IGameInventory gameInventory
            )
        {
            this.logger = logger;
            this.gameInventory = gameInventory;
            this.dataManager = dataManager;

            var luminaExcelModule = dataManager.Excel;
            ItemSheet = luminaExcelModule.GetSheet<Item>() ?? throw new ArgumentException("Item sheet not found");
            ItemSheetEn = luminaExcelModule.GetSheet<Item>(language: Lumina.Data.Language.English) ?? throw new ArgumentException("Item sheet not found");
            ShopSheet = luminaExcelModule.GetSheet<SpecialShop>() ?? throw new InvalidOperationException("Special shop sheet not found");
            Materia = luminaExcelModule.GetSheet<SheetMateria>() ?? throw new InvalidOperationException("Materia sheet not found");
            NameToId = [];
            materiaItemIds = [];
#if DEBUG
            // minimum item id to display for debug logging. Update with new patches to review generations
            // filters out most older items for easier debugging
            uint debugMinItemId = 46000; // lower than most recent to ensure get new items 

            logger.Verbose("Coffer Relations Found");
            foreach (var item in ItemsCoffers)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var itemName = SeStringToString(ItemSheet.GetRow(item.Key).Name);
                foreach (var cofferId in item)
                {
                    var cofferName = SeStringToString(ItemSheet.GetRow(cofferId).Name);
                    logger.Verbose($"{cofferName,-50} => {itemName}");
                }
            }
            logger.Verbose("End Coffer Relations Found");
            logger.Verbose($"Item Prerequisites Found");
            foreach (var item in ItemsPrerequisites)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var recieveItemName = SeStringToString(ItemSheet.GetRow(item.Key).Name);
                foreach (var itemId in item)
                {
                    var prereqItemNames = itemId.Select(id => SeStringToString(ItemSheet.GetRow(id).Name));
                    logger.Verbose($"{string.Join(" + ", prereqItemNames.GroupBy(n => n).Select(g => $"{g.Count()}x {g.Key}")),-60} => {recieveItemName}");
                }
            }
            logger.Verbose($"End Item Prerequisites Found");
#endif
        }
    }

    public interface IItemDataService
    {
        public uint ConvertItemIdToHq(uint id);
        public string SeStringToString(ReadOnlySeString input);
        public string GetItemNameById(uint id);
        public uint GetItemIdByName(string name);
        public List<uint> GetItemMateriaIds(GameInventoryItem item);
        public List<GearMateria> GetItemMateria(GameInventoryItem item);

        /// <summary>
        /// Returns if an item can have materia attached to it
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <returns>If it can be melded. If invalid item id, returns false.</returns>
        public bool ItemIsMeldable(uint itemId);

        /// <summary>   
        /// Extends the given PrerequisiteNode with new leaves at the node's direct child level according to current
        /// data. Used on config loading to automatically add new prerequisite information that wasn't available when the gearset
        /// was being added. Will never remove prerequisites, only add. It will also retain state for old nodes that already
        /// exist in the tree.
        /// </summary>
        /// <param name="itemId">The id of the item to extend prerequisites for</param>
        /// <param name="oldPrerequisiteNode">The old tree of prerequisites for this item</param>
        /// <param name="isCollected">If the config has this node set as collected</param>
        /// <param name="isManuallyCollected">If the config has this node set as manually collected</param>
        /// <returns>The most up-to-date PrerequisiteNode. Could be the unmodified original if no changes were made</returns>
        public IPrerequisiteNode? ExtendItemPrerequisites(
            uint itemId,
            IPrerequisiteNode? oldPrerequisiteNode,
            bool isCollected,
            bool isManuallyCollected
            );

        public IPrerequisiteNode? BuildGearpiecePrerequisiteTree(
            uint itemId,
            bool isCollected = false,
            bool isManuallyCollected = false
            );

        public GearMateria BuildMateria(uint itemId, bool isMelded = false);

        public bool ItemIsShield(uint itemId);

        /// <summary>
        /// Use an item's corresponding ClassJobCategory to return the list of job abbreviations that
        /// can equip the item
        /// </summary>
        /// <param name="itemId">The RowId or HQ-Offset RowId for the item</param>
        /// <returns>The set of 3-letter job abbreviations that can equip the item</returns>
        public HashSet<string> GetItemClassJobCategories(uint itemId);

        /// <summary>
        /// Use an item's corresponding EquipSlotCategory to find it's GearpieceType
        /// </summary>
        /// <param name="itemId">The RowId or HQ-Offset RowId for the item</param>
        /// <returns>The corresponding GearpieceType</returns>
        public GearpieceType GetItemGearpieceType(uint itemId);
    }
}
