using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Util;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services
{
    public class InventoryItemsService(
        ITypedLogger<InventoryItemsService> logger,
        IFramework framework,
        IGameInventory gameInventory,
        IItemDataService itemDataService
        ) : IInventoryItemsService
    {
        private readonly ITypedLogger<InventoryItemsService> logger = logger;
        private readonly IFramework framework = framework;
        private readonly IGameInventory gameInventory = gameInventory;
        private readonly IItemDataService itemDataService = itemDataService;

        public List<InventoryItem> GetInventoryItems(IEnumerable<GameInventoryType> inventories)
        {
            var invItemsTask = framework.RunOnFrameworkThread(() =>
            {
                var itemsList = new List<InventoryItem>();
                foreach (var source in Constants.InventorySources)
                {
                    var items = gameInventory.GetInventoryItems(source);

                    foreach (var item in items)
                    {
                        if (item.ItemId == 0)
                            continue;

                        var itemMateriaIds = getItemMateriaIds(item);
                        for (var i = 0; i < item.Quantity; i++)
                        {
                            var invItem = new InventoryItem(item.ItemId, itemMateriaIds);
                            // add each item in stack individually
                            itemsList.Add(invItem);
                        }
                    }
                }
                return itemsList;
            });

            invItemsTask.WaitSafely();

            return invItemsTask.GetResultSafely();
        }

        private List<uint> getItemMateriaIds(GameInventoryItem item)
        {
            // returns a list of materia ids that are melded to item
            var materiaList = new List<uint>();

            // iterate over materia slots
            for (var i = 0; i < item.Materia.Length; i++)
            {
                var materiaId = item.Materia[i];
                var materiaGrade = item.MateriaGrade[i];

                // no materia in this slot, assume all after are empty
                if (materiaId == 0) break;

                // add item id of materia to list
                try
                {
                    var materiaItemId = itemDataService.GetMateriaItemId(materiaId, materiaGrade);
                    if (materiaItemId != 0) materiaList.Add(materiaItemId);
                }
                catch
                {
                    dumpItemData(item);
                }
            }

            return materiaList;
        }

        private unsafe void dumpItemData(GameInventoryItem item)
        {
            logger.Error($"Dumping item data for {item.ItemId}");
            var data = new byte[0x48];
            var ptr = (byte*)item.Address;
            for (var i = 0; i < 0x48; i++)
            {
                data[i] = ptr[i];
            }
            var str = string.Join(' ', data.Select(t => t.ToString("X")));
            logger.Fatal(str);
        }
    }

    public interface IInventoryItemsService
    {
        public List<InventoryItem> GetInventoryItems(IEnumerable<GameInventoryType> inventories);
    }
}
