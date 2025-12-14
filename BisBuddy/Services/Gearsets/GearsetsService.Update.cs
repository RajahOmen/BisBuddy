using BisBuddy.Extensions;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Util;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BisBuddy.Services.Gearsets
{
    public partial class GearsetsService
    {
        public event GearsetsChangeHandler? OnGearsetsChange;

        /// <summary>
        /// Relates different sort types to the property whos values gearsets should be sorted by.
        /// </summary>
        private static readonly Dictionary<GearsetSortType, PropertyInfo> SortTypePropertyMap = new()
        {
            { GearsetSortType.Name,       getProp(nameof(Gearset.Name)) },
            { GearsetSortType.Job,        getProp(nameof(Gearset.ClassJobAbbreviation)) },
            //{ GearsetSortType.Priority,   getProp(nameof(Gearset.Priority)) },
            { GearsetSortType.ImportDate, getProp(nameof(Gearset.ImportDate)) },
            { GearsetSortType.Active,     getProp(nameof(Gearset.IsActive)) },
        };

        /// <summary>
        /// Describes secondary sorting rules that are applied after the primary sort property.
        /// Change the order of these properties to change their priority in the sort order.
        /// Change descending value to change the direction that property is sorted in
        /// </summary>
        private static readonly List<(PropertyInfo property, bool descending)> SupplementalPropertiesSortOrder =
        [
            ( getProp(nameof(Gearset.IsActive)),             descending: true ),
            //( getProp(nameof(Gearset.Priority)),             descending: true ),
            ( getProp(nameof(Gearset.Name)),                 descending: false ),
            ( getProp(nameof(Gearset.ClassJobAbbreviation)), descending: false ),
            ( getProp(nameof(Gearset.ImportDate)),           descending: false ),
            ( getProp(nameof(Gearset.Id)),                   descending: false ),
        ];

        private static PropertyInfo getProp(string propertyName) =>
            typeof(Gearset).GetProperty(propertyName)!;

        private void triggerGearsetsChange(bool saveToFile = true)
        {
            logger.Verbose($"OnGearsetsChange (saving: {saveToFile})");
            updateItemRequirements();
            sortGearsets(currentGearsetsSortType, currentGearsetsSortDescending);

            framework.RunOnFrameworkThread(() =>
            {
                OnGearsetsChange?.Invoke();
                if (saveToFile)
                    scheduleSaveCurrentGearsets();
            });
        }

        private void handleGearsetChange(bool effectsAssignments)
        {
            // ignore any changes that occur while gearsets are being updated
            if (gearsetsChangeLocked)
                return;

            var updateAssignments = effectsAssignments && configurationService.PluginUpdateInventoryScan;
            scheduleGearsetsChange(updateAssignments: updateAssignments);
        }

        private void scheduleGearsetsChange(bool updateAssignments)
        {
            gearsetsDirty = true;
            assignmentsDirty |= updateAssignments;
        }


        /// <summary>
        /// Coalesce gearset change event calls into one per framework
        /// update to reduce unnecessary updates
        /// </summary>
        private void onUpdate(IFramework framework)
        {
            if (!gearsetsDirty)
                return;

            try
            {
                if (assignmentsDirty)
                    QueueUpdateFromInventory();
                else
                    triggerGearsetsChange(saveToFile: true);
            }
            finally
            {
                gearsetsDirty = false;
                assignmentsDirty = false;
            }
        }

        private void handleLogin()
        {
            logger.Debug($"handling login {clientState.LocalContentId}");
            currentLocalContentId = clientState.LocalContentId;
            loadGearsets();
            if (configurationService.AutoScanInventory)
                QueueUpdateFromInventory();
        }

        private void handleLogout(int type, int code)
        {
            logger.Debug($"handling logout");
            GearsetsLoaded = false;
            currentLocalContentId = 0;
            currentGearsets = [];
        }

        private void handleConfigChange(bool effectsAssignments)
        {
            // the change wouldn't effect how gearsets are assigned
            if (!effectsAssignments)
                return;

            // user has configured this auto update to not occur
            if (!configurationService.PluginUpdateInventoryScan)
                return;

            logger.Debug($"handling config change");
            QueueUpdateFromInventory();
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

            logger.Info($"Adding {gearsetsToAdd.Count()} gearsets to current gearsets");
            scheduleGearsetsChange(updateAssignments: true);
        }

        private void sortGearsets(GearsetSortType sortType, bool sortDescending)
        {
            if (!SortTypePropertyMap.TryGetValue(sortType, out var firstSortProperty))
                throw new NotImplementedException($"Sorting by {Enum.GetName(sortType)} not supported");

            logger.Debug($"Sorting current gearsets by {Enum.GetName(sortType)}, {(sortDescending ? "desc" : "asc")}");

            IEnumerable<Gearset> gearsets = new List<Gearset>(currentGearsets);

            // perform initial ordering based on primary sort property
            var orderedGearsets
                = gearsets.OrderByDirection(firstSortProperty.GetValue, sortDescending);


            // sort via using any remaining tiebreaks
            foreach (var sortProperty in SupplementalPropertiesSortOrder)
            {
                if (sortProperty.property == firstSortProperty)
                    continue;
                orderedGearsets = orderedGearsets.ThenByDirection(sortProperty.property.GetValue, sortProperty.descending);
            }

            currentGearsets = orderedGearsets.ToList();
        }

        public void ChangeGearsetSortOrder(GearsetSortType? newSortType = null, bool? sortDescending = null)
        {
            var sortType = newSortType ?? currentGearsetsSortType;
            var descending = sortDescending ?? currentGearsetsSortDescending;
            if (newSortType != currentGearsetsSortType || sortDescending != currentGearsetsSortDescending)
            {
                sortGearsets(sortType, descending);
                currentGearsetsSortType = sortType;
                currentGearsetsSortDescending = descending;
            }
        }

        public void RemoveGearset(Gearset gearset)
        {
            if (!CurrentGearsets.Contains(gearset))
            {
                logger.Error($"CurrentGearsets does not contain gearset \"{gearset.Id}\" to remove");
                return;
            }

            logger.Info($"Deleting gearset {gearset.Id} ({gearset.Name})");

            gearset.OnGearsetChange -= handleGearsetChange;
            currentGearsets.Remove(gearset);

            scheduleGearsetsChange(updateAssignments: true);
        }

        public void QueueUpdateFromInventory(
            bool saveChanges = true,
            bool manualUpdate = false
            ) => QueueUpdateFromInventory(CurrentGearsets, saveChanges, manualUpdate);

        public void QueueUpdateFromInventory(
            IEnumerable<Gearset> gearsetsToUpdate,
            bool saveChanges = true,
            bool manualUpdate = false
            )
        {
            // do not update if there is no player currently logged in
            if (!GearsetsLoaded)
            {
                logger.Warning($"Tried to update gearsets from inventory (count: {gearsetsToUpdate.Count()}) while not logged in, ignoring request");
                return;
            }

            // display loading state in main menu
            inventoryUpdateDisplayService.UpdateIsQueued = true;
            inventoryUpdateDisplayService.IsManualUpdate = manualUpdate;
            var localContentId = currentLocalContentId;
            var inventoryItemsList = inventoryItemsService.GetInventoryItems(Constants.InventorySources);
            // don't block main thread, queue for execution instead
            var queuedUpdate = queueService.Enqueue($"ASSIGNMENTS_UPDATE_{localContentId}", () =>
            {
                // returns number of gearpiece status changes after update
                try
                {
                    logger.Verbose($"Updating current gearsets with new assignments");
                    gearsetsChangeLocked = true;
                    if (!GearsetsLoaded)
                        return;

                    var solver = itemAssignmentSolverFactory.Create(
                        allGearsets: currentGearsets,
                        assignableGearsets: gearsetsToUpdate,
                        inventoryItems: inventoryItemsList
                        );

                    var updatedGearpieces = solver.SolveAndAssign();

                    logger.Debug($"Updated {updatedGearpieces?.Count ?? 0} gearpieces from inventories");

                    triggerGearsetsChange(saveToFile: saveChanges);

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
                    gearsetsChangeLocked = false;
                }
            });
            if (!queuedUpdate)
            {
                logger.Warning($"QueueService not open when requesting inventory update");
                inventoryUpdateDisplayService.UpdateIsQueued = false;
                inventoryUpdateDisplayService.GearpieceUpdateCount = 0;
            }
        }
    }
}
