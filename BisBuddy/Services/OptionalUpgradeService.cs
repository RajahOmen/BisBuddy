using BisBuddy.Gear;
using BisBuddy.Services.Configuration;
using BisBuddy.Items;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public interface IOptionalUpgradeService : IHostedService
    {
        HighlightColor? GetUpgradeColor(uint itemId);
    }

    public class OptionalUpgradeService(
        IDataManager dataManager,
        IConfigurationService configurationService,
        IInventoryItemsService inventoryItemsService,
        IItemDataService itemDataService,
        IClientState clientState) : IOptionalUpgradeService
    {
        private readonly IDataManager dataManager = dataManager;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IInventoryItemsService inventoryItemsService = inventoryItemsService;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IClientState clientState = clientState;

        // Job ID -> GearpieceType -> List of iLvls (Top 1 or 2)
        private readonly Dictionary<byte, Dictionary<GearpieceType, List<uint>>> bestOwnedItems = [];
        private DateTime lastUpdate = DateTime.MinValue;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void UpdateBestOwnedItems()
        {
            if ((DateTime.Now - lastUpdate).TotalSeconds < 5) return; // Throttling
            lastUpdate = DateTime.Now;

            bestOwnedItems.Clear();
            var items = inventoryItemsService.InventoryItems;
            
            // Pre-fetch all jobs once
            var jobs = dataManager.GetExcelSheet<ClassJob>().Where(j => j.RowId > 0).ToList();

            foreach (var invItem in items)
            {
                var itemRow = dataManager.GetExcelSheet<Item>().GetRowOrDefault(invItem.ItemId);
                if (itemRow == null || itemRow.Value.LevelItem.RowId == 0) continue;

                var type = itemDataService.GetItemGearpieceType(invItem.ItemId);
                if (type == GearpieceType.None) continue;

                var ilvl = itemRow.Value.LevelItem.RowId;
                var classJobCat = itemRow.Value.ClassJobCategory.Value;

                foreach (var job in jobs)
                {
                    if (!IsJobInCategory((byte)job.RowId, classJobCat)) continue;

                    if (!bestOwnedItems.TryGetValue((byte)job.RowId, out var jobMap))
                    {
                        jobMap = [];
                        bestOwnedItems[(byte)job.RowId] = jobMap;
                    }

                    if (!jobMap.TryGetValue(type, out var ilvls))
                    {
                        ilvls = [];
                        jobMap[type] = ilvls;
                    }

                    ilvls.Add(ilvl);
                    ilvls.Sort((a, b) => b.CompareTo(a)); // Descending
                    
                    // We only care about the top 2 for rings, top 1 for everything else
                    int maxCount = (type == GearpieceType.Finger) ? 2 : 1;
                    if (ilvls.Count > maxCount)
                    {
                        ilvls.RemoveAt(maxCount);
                    }
                }
            }
        }

        public HighlightColor? GetUpgradeColor(uint itemId)
        {
            if (!configurationService.HighlightOptionalUpgrades) return null;
            if (!clientState.IsLoggedIn) return null;

            UpdateBestOwnedItems();

            var itemRow = dataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId);
            if (itemRow == null || itemRow.Value.LevelItem.RowId == 0) return null;

            var type = itemDataService.GetItemGearpieceType(itemId);
            if (type == GearpieceType.None) return null;

            var classJobCat = itemRow.Value.ClassJobCategory.Value;
            if (classJobCat.RowId == 0) return null;

            var lootILvl = itemRow.Value.LevelItem.RowId;

            // Check if this item is an upgrade for any job that can wear it
            foreach (var (jobId, jobMap) in bestOwnedItems)
            {
                if (!IsJobInCategory(jobId, classJobCat)) continue;

                if (!jobMap.TryGetValue(type, out var ilvls))
                {
                    return configurationService.OptionalUpgradeColor;
                }

                int requiredCount = (type == GearpieceType.Finger) ? 2 : 1;
                if (ilvls.Count < requiredCount || lootILvl > ilvls.Min())
                {
                    return configurationService.OptionalUpgradeColor;
                }
            }

            return null;
        }

        private bool IsJobInCategory(byte jobId, ClassJobCategory category)
        {
            // Simple check by looking at the ClassJobCategory boolean fields
            // FFXIV stores these as booleans. We can use Lumina's property access.
            var propName = dataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(jobId)?.Abbreviation.ToString();
            if (string.IsNullOrEmpty(propName)) return false;

            var propInfo = typeof(ClassJobCategory).GetProperty(propName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propInfo != null)
            {
                return (bool)(propInfo.GetValue(category) ?? false);
            }
            return false;
        }
    }
}
