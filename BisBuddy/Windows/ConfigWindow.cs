using BisBuddy.Resources;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace BisBuddy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base($"{string.Format(Resource.ConfigWindowTitle, Plugin.PluginName)}###bisbuddyconfiguration")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("General Settings");
        ImGui.Spacing();

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
        {
            // COLOR PICKER
            var existingColor = configuration.DefaultHighlightColor.BaseColor;
            if (ImGui.ColorButton($"{Resource.HighlightColorButtonTooltip}###ColorPickerButton", existingColor))
            {
                ImGui.OpenPopup($"###ColorPickerPopup");
            }

            using (var popup = ImRaii.Popup($"###ColorPickerPopup"))
            {
                if (popup)
                {
                    if (ImGui.ColorPicker4(
                        $"###ColorPicker",
                        ref existingColor,
                        (
                            ImGuiColorEditFlags.NoPicker
                            | ImGuiColorEditFlags.AlphaBar
                            | ImGuiColorEditFlags.NoSidePreview
                            | ImGuiColorEditFlags.DisplayRGB
                            | ImGuiColorEditFlags.NoBorder
                        )))
                    {
                        if (existingColor != configuration.DefaultHighlightColor.BaseColor)
                        {
                            configuration.DefaultHighlightColor.UpdateColor(existingColor);
                            plugin.SaveGearsetsWithUpdate();
                        }
                    }
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(Resource.HighlightColorButtonLabel);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightColorHelp);

        // BRIGHT CUSTOM NODE HIGHLIGHTING
        var brightListItemHighlighting = configuration.BrightListItemHighlighting;
        if (ImGui.Checkbox(Resource.BrightListItemHighlightingCheckbox, ref brightListItemHighlighting))
        {
            configuration.BrightListItemHighlighting = brightListItemHighlighting;
            plugin.SaveGearsetsWithUpdate();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.BrightListItemHighlightingHelp);

        //UNCOLLECTED MATERIA HIGHLIGHTING
        var highlightUncollectedItemMateria = configuration.HighlightUncollectedItemMateria;
        if (ImGui.Checkbox(Resource.HighlightUncollectedItemMateriaCheckbox, ref highlightUncollectedItemMateria))
        {
            configuration.HighlightUncollectedItemMateria = highlightUncollectedItemMateria;
            plugin.SaveGearsetsWithUpdate(false);

        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightUncollectedItemMateriaHelp);

        // ASSIGNMENT GROUPING
        var strictMateriaMatching = configuration.StrictMateriaMatching;
        if (ImGui.Checkbox(Resource.StrictMateriaMatchingCheckbox, ref strictMateriaMatching))
        {
            configuration.StrictMateriaMatching = strictMateriaMatching;
            plugin.SaveConfiguration(true);

            // if auto scanning enabled, rerun assignments with new configuration
            if (configuration.AutoScanInventory)
            {
                plugin.ScheduleUpdateFromInventory(plugin.Gearsets);
            }
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.StrictMateriaMatchingHelp);


        ImGui.Separator();
        ImGui.Text(Resource.ConfigHighlightingSectionHeader);

        // NEED GREED
        var highlightNeedGreed = configuration.HighlightNeedGreed;
        if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
        {
            configuration.HighlightNeedGreed = highlightNeedGreed;
            plugin.SaveConfiguration(false);
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNeedGreedHelp);

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            plugin.SaveConfiguration(false);
            plugin.ShopExchangeItemEventListener.SetListeningStatus(highlightShops);
            plugin.ShopExchangeCurrencyEventListener.SetListeningStatus(highlightShops);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightShopExchangesHelp);

        // MATERIA MELDING
        // toggle highlighting
        var highlightMateriaMeld = configuration.HighlightMateriaMeld;
        if (ImGui.Checkbox(Resource.HighlightMateriaMeldingCheckbox, ref highlightMateriaMeld))
        {
            configuration.HighlightMateriaMeld = highlightMateriaMeld;
            plugin.SaveConfiguration(false);
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightMateriaMeldingHelp);

        // next materia vs all materia
        using (ImRaii.Disabled(!highlightMateriaMeld))
        {
            // draw a L shape for parent-child relationship
            var drawList = ImGui.GetWindowDrawList();
            var curLoc = ImGui.GetCursorScreenPos();
            var col = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));
            var halfButtonHeight = (ImGui.CalcTextSize("HI").Y / 2) + ImGui.GetStyle().FramePadding.Y;
            drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, (halfButtonHeight * 3) + 5), col, 2);
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                var highlightNextMateria = configuration.HighlightNextMateria;
                if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
                {
                    configuration.HighlightNextMateria = highlightNextMateria;
                    plugin.SaveGearsetsWithUpdate(false);
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Resource.HighlightNextMateriaHelp);
            }

            drawList = ImGui.GetWindowDrawList();
            curLoc = ImGui.GetCursorScreenPos();
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                var highlightPrerequisiteMateria = configuration.HighlightPrerequisiteMateria;
                if (ImGui.Checkbox(Resource.HighlightPrerequisiteMateriaCheckbox, ref highlightPrerequisiteMateria))
                {
                    configuration.HighlightPrerequisiteMateria = highlightPrerequisiteMateria;
                    plugin.SaveGearsetsWithUpdate(false);
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Resource.HighlightPrerequisiteMateriaHelp);
            }
        }

        // INVENTORIES
        var highlightInventories = configuration.HighlightInventories;
        if (ImGui.Checkbox(Resource.HighlightInventoriesCheckbox, ref highlightInventories))
        {
            configuration.HighlightInventories = highlightInventories;
            plugin.SaveConfiguration(false);
            plugin.InventoryEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryExpansionEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryBuddyEventListener.SetListeningStatus(highlightInventories);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightInventoriesHelp);

        // MARKETBOARD
        var highlightMarketboard = configuration.HighlightMarketboard;
        if (ImGui.Checkbox(Resource.HighlightMarketboardCheckbox, ref highlightMarketboard))
        {
            configuration.HighlightMarketboard = highlightMarketboard;
            plugin.SaveConfiguration(false);
            plugin.ItemSearchEventListener.SetListeningStatus(highlightMarketboard);
            plugin.ItemSearchResultEventListener.SetListeningStatus(highlightMarketboard);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightMarketboardHelp);

        // ITEM TOOLTIPS
        var annotateTooltips = configuration.AnnotateTooltips;
        if (ImGui.Checkbox(Resource.HighlightItemTooltipsCheckbox, ref annotateTooltips))
        {
            configuration.AnnotateTooltips = annotateTooltips;
            plugin.SaveConfiguration(false);
            plugin.ItemDetailEventListener.SetListeningStatus(annotateTooltips);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightItemTooltipsHelp);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text(Resource.ConfigInventorySectionHeader);
        ImGui.Spacing();

        // ITEM COLLECTION
        var enableAutoComplete = configuration.AutoCompleteItems;
        if (ImGui.Checkbox(Resource.UpdateOnItemChangeCheckbox, ref enableAutoComplete))
        {
            configuration.AutoCompleteItems = enableAutoComplete;
            plugin.SaveConfiguration(true);
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.UpdateOnItemChangeHelp);

        // INVENTORY SCAN ON LOGIN/LOAD
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            plugin.SaveConfiguration(true);
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(string.Format(Resource.UpdateOnLoginLoadHelp, Plugin.PluginName));

        // INVENTORY SCAN ON PLUGIN UPDATES
        var enablePluginUpdateScan = configuration.PluginUpdateInventoryScan;
        if (ImGui.Checkbox(Resource.UpdateOnPluginChangesCheckbox, ref enablePluginUpdateScan))
        {
            configuration.PluginUpdateInventoryScan = enablePluginUpdateScan;
            plugin.SaveConfiguration(true);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(string.Format(Resource.UpdateOnPluginChangesHelp, Plugin.PluginName));
    }
}
