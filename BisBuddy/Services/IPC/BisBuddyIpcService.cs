using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BisBuddy.Gear;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Microsoft.Extensions.Hosting;

namespace BisBuddy.Services.IPC;

/// <summary>
/// Represents a BiS item with its highlight color.
/// </summary>
public record BisItemEntry(uint ItemId, Vector4 Color);

/// <summary>
/// Filter options for retrieving BiS items.
/// </summary>
public record BisItemFilter(
    bool IncludePrereqs = true,
    bool IncludeMateria = true,
    bool IncludeCollected = false,
    bool IncludeObtainable = true,
    bool IncludeCollectedPrereqs = true
);

/// <summary>
/// Represents a registered gearset
/// </summary>
public record BisGearsetEntry(
    string Id,
    string Name,
    uint ClassJobId,
    string ClassJobName,
    string ClassJobAbbreviation,
    bool IsActive
);

/// <summary>
/// Represents a single needed item within a resolved gearset.
/// </summary>
public record BisResolvedItem(
    uint ItemId,
    string RequirementType,
    bool IsCollected
);

public class BisBuddyIpcService : IHostedService
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IGearsetsService gearsetsService;
    private readonly IConfigurationService configurationService;

    private ICallGateProvider<bool>? isInitialized;
    private ICallGateProvider<bool, bool>? initialized;

    private ICallGateProvider<List<BisItemEntry>>? getInventoryHighlightItems;
    private ICallGateProvider<List<BisItemEntry>, bool>? inventoryHighlightItemsChanged;

    private ICallGateProvider<BisItemFilter, List<BisItemEntry>>? getBisItemsFiltered;

    private ICallGateProvider<List<BisGearsetEntry>>? getRegisteredSets;
    private ICallGateProvider<string, BisItemFilter, List<BisResolvedItem>>? resolveSet;

    public BisBuddyIpcService(
        IDalamudPluginInterface pluginInterface,
        IGearsetsService gearsetsService,
        IConfigurationService configurationService)
    {
        this.pluginInterface = pluginInterface;
        this.gearsetsService = gearsetsService;
        this.configurationService = configurationService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        isInitialized = pluginInterface.GetIpcProvider<bool>("BisBuddy.IsInitialized");
        isInitialized.RegisterFunc(() => true);

        initialized = pluginInterface.GetIpcProvider<bool, bool>("BisBuddy.Initialized");

        getInventoryHighlightItems = pluginInterface.GetIpcProvider<List<BisItemEntry>>("BisBuddy.GetInventoryHighlightItems");
        getInventoryHighlightItems.RegisterFunc(GetInventoryHighlightItemsInternal);

        inventoryHighlightItemsChanged = pluginInterface.GetIpcProvider<List<BisItemEntry>, bool>("BisBuddy.InventoryHighlightItemsChanged");

        getBisItemsFiltered = pluginInterface.GetIpcProvider<BisItemFilter, List<BisItemEntry>>("BisBuddy.GetBisItemsFiltered");
        getBisItemsFiltered.RegisterFunc(GetBisItemsFilteredInternal);

        getRegisteredSets = pluginInterface.GetIpcProvider<List<BisGearsetEntry>>("BisBuddy.GetRegisteredSets");
        getRegisteredSets.RegisterFunc(GetRegisteredSetsInternal);

        resolveSet = pluginInterface.GetIpcProvider<string, BisItemFilter, List<BisResolvedItem>>("BisBuddy.ResolveSet");
        resolveSet.RegisterFunc(ResolveSetInternal);

        gearsetsService.OnGearsetsChange += OnGearsetsChanged;
        configurationService.OnConfigurationChange += OnConfigurationChanged;

        initialized.SendMessage(true);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all registered gearsets with their id, name, and job info.
    /// </summary>
    private List<BisGearsetEntry> GetRegisteredSetsInternal()
    {
        return gearsetsService.CurrentGearsets
           .Select(g => new BisGearsetEntry(
                Id: g.Id,
                Name: g.Name,
                ClassJobId: g.ClassJobInfo.ClassJobId,
                ClassJobName: g.ClassJobInfo.Name,
                ClassJobAbbreviation: g.ClassJobInfo.Abbreviation,
                IsActive: g.IsActive
            )).ToList();
    }

    /// <summary>
    /// Resolves a specific gearset by ID and returns its needed items,
    /// respecting the provided filter options.
    /// </summary>
    private List<BisResolvedItem> ResolveSetInternal(string setId, BisItemFilter filter)
    {
        var gearset = gearsetsService.CurrentGearsets
            .FirstOrDefault(g => g.Id == setId);

        if (gearset is null)
            return [];

        var result = new List<BisResolvedItem>();

        foreach (var req in gearset.ItemRequirements(includeUncollectedItemMateria: filter.IncludeMateria))
        {
            var itemReq = req.ItemRequirement;
            var isCollected = itemReq.CollectionStatus >= CollectionStatusType.ObtainedPartial;

            switch (itemReq.RequirementType)
            {
                case RequirementType.Gearpiece:
                    if (!filter.IncludeCollected && isCollected)
                        continue;
                    break;

                case RequirementType.Materia:
                    if (!filter.IncludeMateria)
                        continue;
                    if (!filter.IncludeCollected && isCollected)
                        continue;
                    break;

                case RequirementType.Prerequisite:
                    if (!filter.IncludePrereqs)
                        continue;
                    if (!filter.IncludeCollectedPrereqs && isCollected)
                        continue;
                    break;
            }

            if (!filter.IncludeObtainable
                && itemReq.CollectionStatus == CollectionStatusType.Obtainable)
                continue;

            result.Add(new BisResolvedItem(
                ItemId: itemReq.ItemId,
                RequirementType: itemReq.RequirementType.ToString(),
                IsCollected: isCollected
            ));
        }

        return result;
    }

    /// <summary>
    /// Returns items filtered the same way as vanilla inventory highlighting.
    /// Respects user's HighlightCollectedInInventory setting.
    /// </summary>
    private List<BisItemEntry> GetInventoryHighlightItemsInternal()
    {
        var filter = new BisItemFilter(
            IncludePrereqs: true,
            IncludeMateria: true,
            IncludeCollected: configurationService.HighlightCollectedInInventory,
            IncludeObtainable: true,
            IncludeCollectedPrereqs: true
        );

        return GetBisItemsFilteredInternal(filter);
    }

    /// <summary>
    /// Returns items with consumer-specified filters.
    /// Allows consumers to control exactly what items are returned.
    /// </summary>
    private List<BisItemEntry> GetBisItemsFilteredInternal(BisItemFilter filter)
    {
        var result = new List<BisItemEntry>();

        foreach (var itemId in gearsetsService.AllItemRequirements.Keys)
        {
            var color = gearsetsService.GetRequirementColor(
                itemId,
                includePrereqs: filter.IncludePrereqs,
                includeMateria: filter.IncludeMateria,
                includeCollected: filter.IncludeCollected,
                includeObtainable: filter.IncludeObtainable,
                includeCollectedPrereqs: filter.IncludeCollectedPrereqs
            );

            if (color is not null)
            {
                result.Add(new BisItemEntry(itemId, color.BaseColor));
            }
        }

        return result;
    }

    private void OnGearsetsChanged()
    {
        inventoryHighlightItemsChanged?.SendMessage(GetInventoryHighlightItemsInternal());
    }

    private void OnConfigurationChanged(bool effectsAssignments)
    {
        inventoryHighlightItemsChanged?.SendMessage(GetInventoryHighlightItemsInternal());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        gearsetsService.OnGearsetsChange -= OnGearsetsChanged;
        configurationService.OnConfigurationChange -= OnConfigurationChanged;
        isInitialized?.UnregisterFunc();
        getInventoryHighlightItems?.UnregisterFunc();
        getBisItemsFiltered?.UnregisterFunc();
        getRegisteredSets?.UnregisterFunc();
        resolveSet?.UnregisterFunc();
        initialized?.SendMessage(false);
        return Task.CompletedTask;
    }
}
