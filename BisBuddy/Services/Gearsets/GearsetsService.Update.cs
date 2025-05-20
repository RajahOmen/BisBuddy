using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace BisBuddy.Services.Gearsets
{
    public partial class GearsetsService
    {
        public event GearsetsChangeHandler? OnGearsetsChange;

        private void triggerGearsetsChange()
        {
            gearsets = getCurrentGearsets();
            itemRequirements = buildItemRequirements();
            OnGearsetsChange?.Invoke();
        }

        private void handleLogin()
        {
            gearsets = getCurrentGearsets();
            itemRequirements = buildItemRequirements();
        }

        private void handleLogout(int type, int code)
        {
            gearsets.Clear();
            itemRequirements.Clear();
        }

        public void ScheduleUpdateFromInventory(
            bool saveChanges = true,
            bool manualUpdate = false
            ) => ScheduleUpdateFromInventory(gearsets, saveChanges, manualUpdate);

        public void ScheduleUpdateFromInventory(
            List<Gearset> gearsetsToUpdate,
            bool saveChanges = true,
            bool manualUpdate = false
            )
        {
            // don't block main thread, queue for execution instead
            itemAssignmentQueue.Enqueue(() =>
            {
                // returns number of gearpiece status changes after update
                try
                {
                    if (gearsetsToUpdate.Count == 0) return;

                    // display loading state in main menu
                    mainWindow.InventoryScanRunning = true;

                    var itemsList = getGameInventoryItems();
                    var gearpiecesToUpdate = Gearset.GetGearpiecesFromGearsets(gearsetsToUpdate);

                    var solver = new ItemAssigmentSolver(
                        gearsets.Where(g => g.IsActive).ToList(),
                        gearsetsToUpdate,
                        itemsList,
                        itemData,
                        configService.Config.StrictMateriaMatching,
                        configService.Config.HighlightPrerequisiteMateria
                        );

                    var updatedGearpieces = solver.SolveAndAssign();

                    pluginLog.Debug($"Updated {updatedGearpieces?.Count ?? 0} gearpieces from inventories");

                    triggerGearsetsChange();

                    if (saveChanges)
                        configService.Save();

                    if (manualUpdate)
                        mainWindow.InventoryScanUpdateCount = updatedGearpieces?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    pluginLog.Error(ex, "Failed to update gearsets from inventory");

                    if (manualUpdate)
                    {
                        mainWindow.InventoryScanUpdateCount = 0;
                    }
                }
                finally
                {
                    mainWindow.InventoryScanRunning = false;
                }
            });
        }

        private List<GameInventoryItem> getGameInventoryItems()
        {
            var itemsList = new List<GameInventoryItem>();
            foreach (var source in inventorySources)
            {
                var items = gameInventory.GetInventoryItems(source);

                foreach (var item in items)
                {
                    if (item.ItemId == 0)
                        continue;

                    for (var i = 0; i < item.Quantity; i++)
                    {
                        // add each item in stack individually
                        itemsList.Add(item);
                    }
                }
            }
            return itemsList;
        }

        private void handleItemAdded(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var addedArgs = (InventoryItemAddedArgs)args;

                // not added to a inventory type we track, ignore
                if (!inventorySources.Contains(addedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!RequirementsNeedItemId(
                        addedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // added to type we track, update gearsets
                ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to handle ItemAdded");
            }
        }

        private void handleItemRemoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var removedArgs = (InventoryItemRemovedArgs)args;

                // not removed from a inventory type we track, ignore
                if (!inventorySources.Contains(removedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!RequirementsNeedItemId(
                        removedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // removed from type we track, update gearsets
                ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to handle ItemRemoved");
            }
        }

        private void handleItemChanged(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var changedArgs = (InventoryItemChangedArgs)args;

                // not changed in a inventory type we track, ignore
                if (!inventorySources.Contains(changedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!RequirementsNeedItemId(
                        changedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // changed in a type we track, update gearsets
                ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to handle ItemChanged");
            }
        }

        private void handleItemMoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                var movedArgs = (InventoryItemMovedArgs)args;

                // either untracked -> untracked, or tracked -> tracked. Either way, don't change.
                if (inventorySources.Contains(movedArgs.SourceInventory)
                    == inventorySources.Contains(movedArgs.TargetInventory)
                    )
                    return;

                // item not needed in any gearsets, ignore
                if (!RequirementsNeedItemId(
                        movedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // moved untracked -> tracked or tracked -> untracked, update gearsets
                ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex, "Failed to handle ItemMoved");
            }
        }
    }
}
