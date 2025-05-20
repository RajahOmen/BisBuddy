using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GearMateria = BisBuddy.Gear.Materia;
using SheetMateria = Lumina.Excel.Sheets.Materia;

namespace BisBuddy.Items
{
    public partial class ItemDataService : IItemDataService
    {
        private readonly ExcelModule luminaExcelModule;
        private readonly IPluginLog pluginLog;
        private readonly IGameInventory gameInventory;
        private ILookup<uint, uint>? itemsCoffers = null;
        private ILookup<uint, List<uint>>? itemsPrerequisites = null;

        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';
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

        public ItemDataService(IDataManager dataManager, IPluginLog pluginLog, IGameInventory gameInventory)
        {
            this.pluginLog = pluginLog;
            this.luminaExcelModule = dataManager.Excel;
            this.gameInventory = gameInventory;
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

            pluginLog.Verbose("Coffer Relations Found");
            foreach (var item in ItemsCoffers)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var itemName = SeStringToString(ItemSheet.GetRow(item.Key).Name);
                foreach (var cofferId in item)
                {
                    var cofferName = SeStringToString(ItemSheet.GetRow(cofferId).Name);
                    pluginLog.Verbose($"{cofferName,-50} => {itemName}");
                }
            }
            pluginLog.Verbose("End Coffer Relations Found");
            pluginLog.Verbose($"Item Prerequisites Found");
            foreach (var item in ItemsPrerequisites)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var recieveItemName = SeStringToString(ItemSheet.GetRow(item.Key).Name);
                foreach (var itemId in item)
                {
                    var prereqItemNames = itemId.Select(id => SeStringToString(ItemSheet.GetRow(id).Name));
                    pluginLog.Verbose($"{string.Join(" + ", prereqItemNames.GroupBy(n => n).Select(g => $"{g.Count()}x {g.Key}")),-60} => {recieveItemName}");
                }
            }
            pluginLog.Verbose($"End Item Prerequisites Found");
#endif
        }
    }

    public interface IItemDataService
    {

    }
}
