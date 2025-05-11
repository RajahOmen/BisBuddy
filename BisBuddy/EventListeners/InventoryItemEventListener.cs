using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using System;
using System.Linq;

namespace BisBuddy.EventListeners
{
    public class InventoryItemEventListener : EventListener
    {
        public InventoryItemEventListener(Plugin plugin) : base(plugin)
        {
            if (Plugin.Configuration.AutoCompleteItems)
            {
                register();
            }
        }

        protected override void register()
        {
            Services.GameInventory.ItemAdded += handleItemAdded;
            Services.GameInventory.ItemRemoved += handleItemRemoved;
            Services.GameInventory.ItemChanged += handleItemChanged;
            Services.GameInventory.ItemMoved += handleItemMoved;
        }

        protected override void unregister()
        {
            Services.GameInventory.ItemAdded -= handleItemAdded;
            Services.GameInventory.ItemRemoved -= handleItemRemoved;
            Services.GameInventory.ItemChanged -= handleItemChanged;
            Services.GameInventory.ItemMoved -= handleItemMoved;
        }

        protected override void dispose()
        {
            if (IsEnabled) unregister();
        }

        private void handleItemAdded(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var addedArgs = (InventoryItemAddedArgs)args;

                // not added to a inventory type we track, ignore
                if (!Plugin.InventorySources.Contains(addedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!Gearset.RequirementsNeedItemId(
                        addedArgs.Item.ItemId,
                        Plugin.ItemRequirements,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // added to type we track, update gearsets
                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle ItemAdded");
            }
        }

        private void handleItemRemoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var removedArgs = (InventoryItemRemovedArgs)args;

                // not removed from a inventory type we track, ignore
                if (!Plugin.InventorySources.Contains(removedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!Gearset.RequirementsNeedItemId(
                        removedArgs.Item.ItemId,
                        Plugin.ItemRequirements,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // removed from type we track, update gearsets
                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle ItemRemoved");
            }
        }

        private void handleItemChanged(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var changedArgs = (InventoryItemChangedArgs)args;

                // not changed in a inventory type we track, ignore
                if (!Plugin.InventorySources.Contains(changedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!Gearset.RequirementsNeedItemId(
                        changedArgs.Item.ItemId,
                        Plugin.ItemRequirements,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // changed in a type we track, update gearsets
                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle ItemChanged");
            }
        }

        private void handleItemMoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var movedArgs = (InventoryItemMovedArgs)args;

                // either untracked -> untracked, or tracked -> tracked. Either way, don't change.
                if (Plugin.InventorySources.Contains(movedArgs.SourceInventory) == Plugin.InventorySources.Contains(movedArgs.TargetInventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!Gearset.RequirementsNeedItemId(
                        movedArgs.Item.ItemId,
                        Plugin.ItemRequirements,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // moved untracked -> tracked or tracked -> untracked, update gearsets
                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle ItemMoved");
            }
        }
    }
}
