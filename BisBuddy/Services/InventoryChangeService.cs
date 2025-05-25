using BisBuddy.Services.Config;
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
        IGameInventory gameInventory,
        IGearsetsService gearsetsService,
        IConfigurationService configurationService
        ) : IInventoryChangeService
    {
        private readonly ITypedLogger<InventoryChangeService> logger = logger;
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
                var changedArgs = (InventoryItemChangedArgs)args;

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
