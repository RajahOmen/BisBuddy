using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Util;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services
{
    public class InventoryItemsService : IInventoryItemsService, IDisposable
    {
        private readonly ITypedLogger<InventoryItemsService> logger;
        private readonly IFramework framework;
        private readonly IClientState clientState;
        private readonly IGameInventory gameInventory;
        private readonly IItemDataService itemDataService;

        private Dictionary<uint, (InventoryItem Item, int Count)> inventoryItemsCountCache = [];
        private List<InventoryItem> inventoryItemsListCache = [];

        public IReadOnlyDictionary<uint, (InventoryItem Item, int Count)> ItemInventoryQuantities =>
            inventoryItemsCountCache;
        public IReadOnlyList<InventoryItem> InventoryItems =>
            inventoryItemsListCache;

        public InventoryItemsService(
            ITypedLogger<InventoryItemsService> logger,
            IFramework framework,
            IClientState clientState,
            IGameInventory gameInventory,
            IItemDataService itemDataService
        )
        {
            this.logger = logger;
            this.framework = framework;
            this.clientState = clientState;
            this.gameInventory = gameInventory;
            this.itemDataService = itemDataService;

            this.clientState.Login += handleLogin;
            this.clientState.Logout += handleLogout;
            this.gameInventory.InventoryChangedRaw += handleInventoryChanged;

            if (clientState.IsLoggedIn)
                updateInventoryItemsCache();
        }

        public void Dispose()
        {
            this.clientState.Login -= handleLogin;
            this.clientState.Logout -= handleLogout;
            this.gameInventory.InventoryChanged -= handleInventoryChanged;
        }

        private void handleInventoryChanged(IReadOnlyCollection<InventoryEventArgs> _) =>
            updateInventoryItemsCache();

        private void handleLogin() =>
            updateInventoryItemsCache();

        private void handleLogout(int type, int code)
        {
            inventoryItemsCountCache.Clear();
            inventoryItemsListCache.Clear();
        }

        private void updateInventoryItemsCache()
        {
            Dictionary<uint, (InventoryItem Item, int Count)> newCountCache = [];
            List<InventoryItem> newListCache = [];
            var invItemsTask = framework.RunOnFrameworkThread(() =>
            {
                foreach (var source in Constants.InventorySources)
                {
                    var items = gameInventory.GetInventoryItems(source);

                    foreach (var item in items)
                    {
                        if (item.ItemId == 0)
                            continue;

                        var itemMateriaIds = getItemMateriaIds(item);
                        var invItem = new InventoryItem(item.ItemId, itemMateriaIds);

                        var (_, existingCount) = newCountCache.GetValueOrDefault(invItem.ItemId);
                        newCountCache[invItem.ItemId] = (invItem, existingCount + item.Quantity);

                        // add each item in stack individually
                        for (var i = 0; i < item.Quantity; i++)
                            newListCache.Add(invItem.Copy());
                    }
                }
                inventoryItemsCountCache = newCountCache;
                inventoryItemsListCache = newListCache;
            });
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
        IReadOnlyList<InventoryItem> InventoryItems { get; }
        IReadOnlyDictionary<uint, (InventoryItem Item, int Count)> ItemInventoryQuantities { get; }
    }
}
