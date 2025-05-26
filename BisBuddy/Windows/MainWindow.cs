using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Util;
using BisBuddy.Windows.Config;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.Windows;

public partial class MainWindow : Window, IDisposable
{
    private readonly ITypedLogger<MainWindow> logger;
    private readonly IClientState clientState;
    private readonly ConfigWindow configWindow;
    private readonly ImportGearsetWindow importGearsetWindow;
    private readonly IGearsetsService gearsetsService;
    private readonly IInventoryUpdateDisplayService inventoryUpdateService;
    private readonly IConfigurationService configurationService;

    public static readonly Vector4 UnobtainedColor = new(1.0f, 0.2f, 0.2f, 1.0f);
    public static readonly Vector4 ObtainedColor = new(0.2f, 1.0f, 0.2f, 1.0f);

    public MainWindow(
        ITypedLogger<MainWindow> logger,
        IClientState clientState,
        ConfigWindow configWindow,
        ImportGearsetWindow importGearsetWindow,
        IGearsetsService gearsetsService,
        IInventoryUpdateDisplayService inventoryUpdateService,
        IConfigurationService configurationService
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
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(530, 250),
            MaximumSize = new Vector2(1000, float.MaxValue)
        };
        Size = new Vector2(530, 500);
        SizeCondition = ImGuiCond.Appearing;
    }

    public override void OnClose()
    {
        base.OnClose();
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

    private void drawHeader()
    {
        var updateIsQueued = inventoryUpdateService.UpdateIsQueued;
        var manualUpdatedCount = inventoryUpdateService.IsManualUpdate
            ? inventoryUpdateService.GearpieceUpdateCount
            : -1;

        using (ImRaii.Disabled(!clientState.IsLoggedIn))
        {
            using (ImRaii.Disabled(gearsetsService.CurrentGearsets.Count >= Constants.MaxGearsetCount))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"{Resource.NewGearsetButton}##importgearset"))
                {
                    if (clientState.IsLoggedIn)
                        importGearsetWindow.Toggle();
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip =
                        gearsetsService.CurrentGearsets.Count >= Constants.MaxGearsetCount
                        ? string.Format(Resource.NewGearsetTooltipMaxGearsets, Constants.MaxGearsetCount)
                        : Resource.NewGearsetTooltip;
                    ImGui.SetTooltip(tooltip);
                }
            }

            ImGui.SameLine();

            using (ImRaii.Disabled(updateIsQueued || gearsetsService.CurrentGearsets.Count == 0))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, $"{Resource.SyncInventoryButton}##scaninventory"))
                    gearsetsService.ScheduleUpdateFromInventory(manualUpdate: true);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Resource.SyncInventoryTooltip);
            }
        }
        if (updateIsQueued)
        {
            ImGui.SameLine();
            ImGui.Text(Resource.InventoryScanLoading);
        }
        else if (manualUpdatedCount >= 0)
        {
            ImGui.SameLine();
            ImGui.Text(string.Format(Resource.InventoryScanUpdated, manualUpdatedCount));
        }

        ImGui.SameLine();

        var configButtonSize = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.Cog, "");
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - (configButtonSize + 12));
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            configWindow.Toggle();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.OpenConfigTooltip);
    }

    private void drawNoGearsets()
    {
        var errorText = clientState.IsLoggedIn
            ? Resource.NoGearsetsText
            : Resource.LoggedOutText;
        var textWidth = ImGui.CalcTextSize(errorText).X;
        var offsetX = (ImGui.GetWindowWidth() - textWidth) * 0.5f;
        ImGui.SetCursorPosX(offsetX); // Center text
        ImGui.Text(errorText);
    }

    private void drawGearsets(IReadOnlyList<Gearset> gearsets)
    {
        var gearsetsToDelete = new List<Gearset>();

        for (var i = 0; i < gearsets.Count; i++)
        {
            var gearset = gearsets[i];
            using (ImRaii.PushId(gearset.Id))
            {
                var deleteGearset = drawGearset(gearset);
                if (deleteGearset)
                {
                    gearsetsToDelete.Add(gearset);
                }
            }
        }

        if (gearsetsToDelete.Count > 0)
        {
            foreach (var gearset in gearsetsToDelete)
            {
                logger.Verbose($"Removed gearset \"{gearset.Name}\"");
                gearsetsService.RemoveGearset(gearset);
            }
        }
    }

    public override void Draw()
    {
        var headerWidth = ImGui.GetWindowWidth();
        var headerHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2;
        using (
            ImRaii.Child(
                $"###main_header",
                new Vector2(headerWidth, headerHeight),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
                )
            )
        {
            drawHeader();
        }
        ImGui.Spacing();
        ImGui.Separator();

        var childWidth = ImGui.GetWindowWidth() - 12;
        using (
            ImRaii.Child(
                $"###main_content",
                new Vector2(childWidth, 0),
                false,
                ImGuiWindowFlags.AlwaysAutoResize
                )
            )
        {
            if (gearsetsService.CurrentGearsets.Count == 0)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();

                drawNoGearsets();
            }
            else
            {
                ImGui.Spacing();
                drawGearsets(gearsetsService.CurrentGearsets);
            }
        }
    }
}
