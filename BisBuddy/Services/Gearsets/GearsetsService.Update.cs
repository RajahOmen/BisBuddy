using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Util;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BisBuddy.Services.Gearsets
{
    public partial class GearsetsService
    {
        public event GearsetsChangeHandler? OnGearsetsChange;

        private void triggerGearsetsChange(bool saveToFile = true)
        {
            logger.Verbose($"OnGearsetsChange (saving: {saveToFile})");
            updateItemRequirements();
            OnGearsetsChange?.Invoke();
            if (saveToFile)
                scheduleSaveCurrentGearsets();
        }

        private void handleGearsetChange() =>
            scheduleGearsetsChange();

        private void scheduleGearsetsChange() =>
            gearsetsDirty = true;

        /// <summary>
        /// Coalesce gearset change event calls into one per framework
        /// update to reduce unnecessary updates
        /// </summary>
        private void onUpdate(IFramework framework)
        {
            if (!gearsetsDirty)
                return;

            gearsetsDirty = false;
            triggerGearsetsChange(saveToFile: true);
        }

        private void handleLogin()
        {
            loadGearsets();
            if (configurationService.AutoScanInventory)
                ScheduleUpdateFromInventory();
        }

        private void handleLogout(int type, int code)
            => currentGearsets = [];

        private void handleConfigChange(bool effectsAssignments)
        {
            // the change wouldn't effect how gearsets are assigned
            if (!effectsAssignments)
                return;

            // user has configured this auto update to not occur
            if (!configurationService.PluginUpdateInventoryScan)
                return;

            ScheduleUpdateFromInventory();
        }

        public async Task<ImportGearsetsResult> AddGearsetsFromSource(ImportGearsetSourceType sourceType, string sourceString)
        {
            var gearsetCapacity = Constants.MaxGearsetCount - CurrentGearsets.Count;
            var importResult = await importGearsetService.ImportGearsets(sourceType, sourceString, gearsetCapacity);

            if (importResult.StatusType != GearsetImportStatusType.Success)
                return importResult;

            if (importResult.Gearsets is List<Gearset> gearsetsToAdd)
                addGearsets(gearsetsToAdd);
            else
                logger.Error($"Tried to add null gearsets from source");

            return importResult;
        }

        private void addGearsets(IEnumerable<Gearset> gearsetsToAdd)
        {
            foreach (var gearset in gearsetsToAdd)
            {
                currentGearsets.Add(gearset);
                gearset.OnGearsetChange += handleGearsetChange;
            }

            if (configurationService.PluginUpdateInventoryScan)
                ScheduleUpdateFromInventory();
            else
                scheduleGearsetsChange();
        }

        public void RemoveGearset(Gearset gearset)
        {
            if (!CurrentGearsets.Contains(gearset))
            {
                logger.Error($"CurrentGearsets does not contain gearset \"{gearset.Id}\" to remove");
                return;
            }

            gearset.OnGearsetChange -= handleGearsetChange;
            currentGearsets.Remove(gearset);

            if (configurationService.PluginUpdateInventoryScan)
                ScheduleUpdateFromInventory();
            else
                scheduleGearsetsChange();
        }

        public void ScheduleUpdateFromInventory(
            bool saveChanges = true,
            bool manualUpdate = false
            ) => ScheduleUpdateFromInventory(CurrentGearsets, saveChanges, manualUpdate);

        public void ScheduleUpdateFromInventory(
            IEnumerable<Gearset> gearsetsToUpdate,
            bool saveChanges = true,
            bool manualUpdate = false
            )
        {
            // display loading state in main menu
            inventoryUpdateDisplayService.UpdateIsQueued = true;
            inventoryUpdateDisplayService.IsManualUpdate = manualUpdate;

            // don't block main thread, queue for execution instead
            var queuedUpdate = queueService.Enqueue(() =>
            {
                // returns number of gearpiece status changes after update
                try
                {
                    logger.Verbose($"Updating current gearsets with new assignments");
                    if (!clientState.IsLoggedIn)
                        return;

                    var itemsList = getGameInventoryItems();
                    var gearpiecesToUpdate = Gearset.GetGearpiecesFromGearsets(gearsetsToUpdate);

                    var solver = itemAssignmentSolverFactory.Create(
                        allGearsets: currentGearsets.Where(g => g.IsActive),
                        assignableGearsets: gearsetsToUpdate,
                        inventoryItems: itemsList
                        );

                    var updatedGearpieces = solver.SolveAndAssign();

                    logger.Debug($"Updated {updatedGearpieces?.Count ?? 0} gearpieces from inventories");

                    scheduleGearsetsChange();

                    inventoryUpdateDisplayService.GearpieceUpdateCount = updatedGearpieces?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to update gearsets from inventory");
                    inventoryUpdateDisplayService.GearpieceUpdateCount = 0;
                }
                finally
                {
                    inventoryUpdateDisplayService.UpdateIsQueued = false;
                }
            });
            if (!queuedUpdate)
            {
                logger.Warning($"QueueService not open when requesting inventory update");
                inventoryUpdateDisplayService.UpdateIsQueued = false;
                inventoryUpdateDisplayService.GearpieceUpdateCount = 0;
            }
        }

        private List<GameInventoryItem> getGameInventoryItems()
        {
            var itemsList = new List<GameInventoryItem>();
            foreach (var source in Constants.InventorySources)
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
    }
}
