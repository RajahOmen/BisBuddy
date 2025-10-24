using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Util;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class InventoryChangeService(
        ITypedLogger<InventoryChangeService> logger,
        IClientState clientState,
        IGameInventory gameInventory,
        IGearsetsService gearsetsService,
        IConfigurationService configurationService
        ) : IInventoryChangeService
    {
        private readonly ITypedLogger<InventoryChangeService> logger = logger;
        private readonly IClientState clientState = clientState;
        private readonly IGameInventory gameInventory = gameInventory;
        private readonly IGearsetsService gearsetsService = gearsetsService;
        private readonly IConfigurationService configurationService = configurationService;

        private bool isListening = false;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            configurationService.OnConfigurationChange += onConfigChange;
            updateListeningStatus(configurationService.AutoCompleteItems);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            configurationService.OnConfigurationChange -= onConfigChange;
            updateListeningStatus(false);

            return Task.CompletedTask;
        }

        private void onConfigChange(bool effectsAssignments)
            => updateListeningStatus(configurationService.AutoCompleteItems);

        private void updateListeningStatus(bool toListen)
        {
            if (toListen == isListening)
                return;

            logger.Verbose($"{(toListen ? "Registering" : "Unregistering")} listeners");

            if (toListen)
            {
                gameInventory.ItemAdded += handleItemAdded;
                gameInventory.ItemRemoved += handleItemRemoved;
                gameInventory.ItemChanged += handleItemChanged;
                gameInventory.ItemMoved += handleItemMoved;
                isListening = true;
            }
            else
            {
                gameInventory.ItemAdded -= handleItemAdded;
                gameInventory.ItemRemoved -= handleItemRemoved;
                gameInventory.ItemChanged -= handleItemChanged;
                gameInventory.ItemMoved -= handleItemMoved;
                isListening = false;
            }
        }

        private void handleItemAdded(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                // if gearsets haven't been loaded, ignore
                if (!gearsetsService.GearsetsLoaded)
                    return;

                var addedArgs = (InventoryItemAddedArgs)args;

                // not added to a inventory type we track, ignore
                if (!Constants.InventorySources.Contains(addedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!gearsetsService.RequirementsNeedItemId(
                        addedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // added to type we track, update gearsets
                logger.Verbose($"item added, scehduling gearset update");
                gearsetsService.ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to handle ItemAdded");
            }
        }

        private void handleItemRemoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                // if gearsets haven't been loaded, ignore
                if (!gearsetsService.GearsetsLoaded)
                    return;

                var removedArgs = (InventoryItemRemovedArgs)args;

                // not removed from a inventory type we track, ignore
                if (!Constants.InventorySources.Contains(removedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!gearsetsService.RequirementsNeedItemId(
                        removedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                // removed from type we track, update gearsets
                logger.Verbose($"item removed, scehduling gearset update");
                gearsetsService.ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to handle ItemRemoved");
            }
        }

        private void handleItemChanged(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                // if gearsets haven't been loaded, ignore
                if (!gearsetsService.GearsetsLoaded)
                    return;

                var changedArgs = (InventoryItemChangedArgs)args;

                var oldMateria = changedArgs.OldItemState.MateriaEntries.ToArray();
                var newMateria = changedArgs.Item.MateriaEntries.ToArray();
                if (changedArgs.OldItemState.ItemId == changedArgs.Item.ItemId
                    && newMateria.SequenceEqual(oldMateria))
                {
                    logger.Verbose($"Item changed, but no relevant changes detected, ignoring");
                    return;
                }

                // not changed in a inventory type we track, ignore
                if (!Constants.InventorySources.Contains(changedArgs.Inventory))
                    return;

                // item not needed in any gearsets, ignore
                if (!gearsetsService.RequirementsNeedItemId(
                        changedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;
                logger.Verbose($"item changed, scehduling gearset update");
                // changed in a type we track, update gearsets
                gearsetsService.ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to handle ItemChanged");
            }
        }

        private void handleItemMoved(GameInventoryEvent type, InventoryEventArgs args)
        {
            try
            {
                // if gearsets haven't been loaded, ignore
                if (!gearsetsService.GearsetsLoaded)
                    return;

                var movedArgs = (InventoryItemMovedArgs)args;

                // either untracked -> untracked, or tracked -> tracked. Either way, don't change.
                if (Constants.InventorySources.Contains(movedArgs.SourceInventory)
                    == Constants.InventorySources.Contains(movedArgs.TargetInventory)
                    )
                    return;

                // item not needed in any gearsets, ignore
                if (!gearsetsService.RequirementsNeedItemId(
                        movedArgs.Item.ItemId,
                        includeCollected: true,
                        includeObtainable: true,
                        includeCollectedPrereqs: true
                    ))
                    return;

                logger.Verbose($"item moved, scehduling gearset update");
                // moved untracked -> tracked or tracked -> untracked, update gearsets
                gearsetsService.ScheduleUpdateFromInventory();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to handle ItemMoved");
            }
        }
    }

    public interface IInventoryChangeService : IHostedService
    {

    }
}
