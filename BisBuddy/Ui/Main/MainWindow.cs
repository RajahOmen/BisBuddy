using Autofac.Features.Indexed;
using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Ui.Main.Tabs;
using BisBuddy.Util;
using BisBuddy.Ui.Config;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using BisBuddy.Extensions;
using BisBuddy.Ui.Components;

namespace BisBuddy.Ui;

public class MainWindow : Window, IDisposable
{
    private static readonly WindowSizeConstraints MainSizeConstraints = new()
    {
        MinimumSize = new Vector2(405, 100),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
    };
    private static readonly Vector2 DefaultSize = new(630, 500);

    private readonly ITypedLogger<MainWindow> logger;
    private readonly IClientState clientState;
    private readonly ConfigWindow configWindow;
    private readonly ImportGearsetWindow importGearsetWindow;
    private readonly IGearsetsService gearsetsService;
    private readonly IInventoryUpdateDisplayService inventoryUpdateService;
    private readonly IConfigurationService configurationService;
    private readonly IIndex<MainWindowTab, TabRenderer> tabRendererIndex;
    private readonly IAttributeService attributeService;

    // what tab to currently render
    private MainWindowTab? activeTab = null;
    private MainWindowTab? nextActiveTab = MainWindowTab.UserGearsets;

    // order using the value of the enum as a sort key
    private readonly IEnumerable<MainWindowTab> tabTypes = Enum.GetValues<MainWindowTab>().OrderBy(t => (int) t);

    public MainWindow(
        ITypedLogger<MainWindow> logger,
        IClientState clientState,
        ConfigWindow configWindow,
        ImportGearsetWindow importGearsetWindow,
        IGearsetsService gearsetsService,
        IInventoryUpdateDisplayService inventoryUpdateService,
        IConfigurationService configurationService,
        IIndex<MainWindowTab, TabRenderer> tabRendererIndex,
        IAttributeService attributeService
        )
        : base($"{Constants.PluginName}##bisbuddymainwindow")
    {
        this.logger = logger;
        this.clientState = clientState;
        this.configWindow = configWindow;
        this.importGearsetWindow = importGearsetWindow;
        this.gearsetsService = gearsetsService;
        this.inventoryUpdateService = inventoryUpdateService;
        this.configurationService = configurationService;
        this.tabRendererIndex = tabRendererIndex;
        this.attributeService = attributeService;
        SizeConstraints = MainSizeConstraints;
        Size = DefaultSize;
        SizeCondition = ImGuiCond.Appearing;
    }

    private unsafe void searchItemById(uint itemId)
    {
        try
        {
            logger.Debug($"Searching for item \"{itemId}\"");
            ItemFinderModule.Instance()->SearchForItem(itemId, false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error searching for \"{itemId}\"");
        }
    }

    public void Dispose() { }

    public void OpenToTab(MainWindowTab tabTypeToOpen, TabState? tabState = null)
    {
        if (tabRendererIndex.TryGetValue(tabTypeToOpen, out var tabRenderer))
        {
            if (tabState is not null)
                tabRenderer.SetTabState(tabState);
            nextActiveTab = tabTypeToOpen;
        }
    }

    public override void PreDraw()
    {
        base.PreDraw();

        // perform predraw step for tab that is about to be rendered
        var nextTab = nextActiveTab ?? activeTab;

        if (
            nextTab is MainWindowTab tab
            && tabRendererIndex.TryGetValue(tab, out var nextTabRenderer)
            )
            nextTabRenderer.PreDraw();
    }

    public override void Draw()
    {
        // Appended ##{num} to update the order if I want to change the tab order in future
        // as dalamud will not update the order unless the name changes
        using var tabBar = ImRaii.TabBar("mainWindowTab##0");
        if (!tabBar)
            return;

        foreach (var tabType in tabTypes)
        {
            var tabTitle = attributeService
                .GetEnumAttribute<DisplayAttribute>(tabType)!
                .GetName()!;
            var isActiveFlag = nextActiveTab == tabType
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            using var tabItem = ImRaii.TabItem(tabTitle, isActiveFlag);

            if (tabItem)
                if (tabRendererIndex.TryGetValue(tabType, out var tabRenderer))
                {
                    setSizeConstraints(tabRenderer.TabSizeConstraints);
                    tabRenderer.Draw();
                    activeTab = tabType;
                }
        }

        nextActiveTab = null;
    }

    private void setSizeConstraints(WindowSizeConstraints? tabSizeConstraints)
    {
        if (tabSizeConstraints is not WindowSizeConstraints next)
            return;

        var minSize = new Vector2(
            x: Math.Max(next.MinimumSize.X, MainSizeConstraints.MinimumSize.X),
            y: Math.Max(next.MinimumSize.Y, MainSizeConstraints.MinimumSize.Y)
            );

        var maxSize = new Vector2(
            x: Math.Min(next.MaximumSize.X, MainSizeConstraints.MaximumSize.X),
            y: Math.Min(next.MaximumSize.Y, MainSizeConstraints.MaximumSize.Y)
            );

        SizeConstraints = new()
        {
            MinimumSize = minSize,
            MaximumSize = maxSize
        };
    }

    //private void drawHeader()
    //{
    //    var updateIsQueued = inventoryUpdateService.UpdateIsQueued;
    //    var manualUpdatedCount = inventoryUpdateService.IsManualUpdate
    //        ? inventoryUpdateService.GearpieceUpdateCount
    //        : -1;

    //    using (ImRaii.Disabled(!clientState.IsLoggedIn))
    //    {
    //        using (ImRaii.Disabled(gearsetsService.CurrentGearsets.Count >= Constants.MaxGearsetCount))
    //        {
    //            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"{Resource.NewGearsetButton}##importgearset"))
    //            {
    //                if (clientState.IsLoggedIn)
    //                    importGearsetWindow.Toggle();
    //            }
    //            if (ImGui.IsItemHovered())
    //            {
    //                var tooltip =
    //                    gearsetsService.CurrentGearsets.Count >= Constants.MaxGearsetCount
    //                    ? string.Format(Resource.NewGearsetTooltipMaxGearsets, Constants.MaxGearsetCount)
    //                    : Resource.NewGearsetTooltip;
    //                ImGui.SetTooltip(tooltip);
    //            }
    //        }

    //        ImGui.SameLine();

    //        using (ImRaii.Disabled(updateIsQueued || gearsetsService.CurrentGearsets.Count == 0))
    //        {
    //            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, $"{Resource.SyncInventoryButton}##scaninventory"))
    //                gearsetsService.ScheduleUpdateFromInventory(manualUpdate: true);
    //            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resource.SyncInventoryTooltip);
    //        }
    //    }
    //    if (updateIsQueued)
    //    {
    //        ImGui.SameLine();
    //        ImGui.Text(Resource.InventoryScanLoading);
    //    }
    //    else if (manualUpdatedCount >= 0)
    //    {
    //        ImGui.SameLine();
    //        ImGui.Text(string.Format(Resource.InventoryScanUpdated, manualUpdatedCount));
    //    }

    //    ImGui.SameLine();

    //    var configButtonSize = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.Cog, "");
    //    ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - (configButtonSize + 12));
    //    if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
    //        configWindow.Toggle();
    //    if (ImGui.IsItemHovered())
    //        ImGui.SetTooltip(Resource.OpenConfigTooltip);
    //}

    //private void drawNoGearsets()
    //{
    //    var errorText = clientState.IsLoggedIn
    //        ? Resource.NoGearsetsText
    //        : Resource.LoggedOutText;
    //    var textWidth = ImGui.CalcTextSize(errorText).X;
    //    var offsetX = (ImGui.GetWindowWidth() - textWidth) * 0.5f;
    //    ImGui.SetCursorPosX(offsetX); // Center text
    //    ImGui.Text(errorText);
    //}

    //private void drawGearsets(IReadOnlyList<Gearset> gearsets)
    //{
    //    var gearsetsToDelete = new List<Gearset>();

    //    for (var i = 0; i < gearsets.Count; i++)
    //    {
    //        var gearset = gearsets[i];
    //        using (ImRaii.PushId(gearset.Id))
    //        {
    //            var deleteGearset = drawGearset(gearset);
    //            if (deleteGearset)
    //            {
    //                gearsetsToDelete.Add(gearset);
    //            }
    //        }
    //    }

    //    if (gearsetsToDelete.Count > 0)
    //    {
    //        foreach (var gearset in gearsetsToDelete)
    //        {
    //            logger.Verbose($"Removed gearset \"{gearset.Name}\"");
    //            gearsetsService.RemoveGearset(gearset);
    //        }
    //    }
    //}
}
